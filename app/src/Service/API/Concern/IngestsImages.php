<?php
declare(strict_types=1);

namespace App\Service\API\Concern;

use App\Service\API\DatasetRef;
use League\Flysystem\UnableToReadFile;
use Symfony\Contracts\Cache\ItemInterface;

/**
 * Image ingestion shared by the DDragon resource managers: fetching the images a
 * slice is missing, storing them content-addressed, and recording them in the
 * per-(version,type) manifest that {@see ResolvesImages} then reads back. Split
 * out of {@see AbstractManager} so the write path — with its batching, its
 * progress reporting and its read-merge-write ledger — stays separable from the
 * read path.
 *
 * Composed onto {@see AbstractManager}: uses its gateway/blob-store/storage
 * collaborators, and the slice seams of {@see PaginatesResources}. Holds the
 * warmup pair of {@see WarmableManagerInterface}: {@see collectPlan} prices the
 * work, {@see ingest} performs it.
 */
trait IngestsImages
{
    /**
     * Ingest images in small batches rather than one blocking call. Two reasons:
     *  - the SSE loader gets a stored-name event per batch as it lands, instead of
     *    silence until the whole set is fetched (otherwise the bar sits at 0%);
     *  - each batch is merged into the manifest against fresh storage state, which
     *    bounds the read-modify-write race window to a single batch.
     */
    private const INGEST_CHUNK_SIZE = 12;

    /** @var array<string,array<string,string>> in-request manifest memo, keyed by storage key */
    private array $manifestCache = [];

    /**
     * Cost of warming the images of a page slice, computed without fetching:
     * the full entry map plus how many of those images are not yet stored.
     * Backs the streaming loader's determinate progress total.
     *
     * @return array{entries: array<string,string>, missing: int}
     */
    public function collectPlan(DatasetRef $dataset, int $perPage, int $page): array
    {
        $list  = $this->dataList($this->dataset($dataset));
        $slice = $perPage <= 0
            ? $list
            : $this->slicePage($perPage, $page <= 1 ? 0 : $perPage * ($page - 1), $list);

        $entries  = $this->imageEntries($slice);
        $manifest = $this->loadManifest($dataset->version);
        $missing  = 0;
        foreach (array_keys($entries) as $image) {
            if (!isset($manifest[$image])) {
                $missing++;
            }
        }

        return ['entries' => $entries, 'missing' => $missing];
    }

    /**
     * Synchronously fetch + store the still-missing images of a pre-computed
     * entry map, reporting each stored entry's display name via $onStored. Used
     * by the streaming loader ({@see \App\Controller\Resource\LoaderController}) to warm a
     * destination inline while emitting live progress — never deferred.
     *
     * @param array<string,string> $entries imageFileName => display name
     * @param callable(string):void $onStored invoked with each stored display name
     */
    public function ingest(string $version, array $entries, callable $onStored): void
    {
        $manifest = $this->loadManifest($version);
        $missing  = []; // ddragon url => image name
        foreach (array_keys($entries) as $image) {
            if (!isset($manifest[$image])) {
                $missing[$this->imageUrl($version, $image)] = $image;
            }
        }

        if ($missing === []) {
            return;
        }

        $this->ingestMissing(
            $version,
            $missing,
            static fn (string $image): mixed => $onStored($entries[$image] ?? $image),
        );
    }

    /**
     * Fetch the missing images through the gateway, store them content-addressed
     * (dedup + WebP sibling) and record them in the manifest, in
     * {@see self::INGEST_CHUNK_SIZE}-sized batches: without $onStored (page
     * render, CLI warmup) the outcome is identical to a single pass; with it,
     * each batch reports its stored names as it lands so the loader progresses
     * throughout the network phase instead of only at the end.
     *
     * @param array<string,string> $missing    ddragon url => name
     * @param (callable(string):void)|null $onStored invoked with each image name as it lands
     * @return array<string,string> name => cdn path (only the ones fetched)
     */
    private function ingestMissing(
        string $version,
        array $missing,
        ?callable $onStored = null
    ): array {
        $resolved = [];

        foreach (array_chunk($missing, self::INGEST_CHUNK_SIZE, true) as $chunk) {
            $stored = $this->storeChunk($chunk, $onStored);

            // Persist per batch so progress is durable and the manifest merge
            // (see saveManifest) happens against the freshest storage state.
            if ($stored !== []) {
                $this->saveManifest($version, $stored);
                $resolved += $stored;
            }
        }

        return $resolved;
    }

    /**
     * Fetch one batch in parallel and store whatever came back. Images the
     * gateway could not deliver are simply absent from the result — a definitive
     * upstream absence is not an error here.
     *
     * @param array<string,string> $chunk ddragon url => name
     * @param (callable(string):void)|null $onStored
     * @return array<string,string> name => cdn path
     */
    private function storeChunk(array $chunk, ?callable $onStored): array
    {
        $stored     = [];
        $bytesByUrl = $this->goFetcher->fetchMany(array_keys($chunk));

        foreach ($chunk as $url => $name) {
            if (!isset($bytesByUrl[$url])) {
                continue;
            }
            $stored[$name] = $this->blobStore->store($bytesByUrl[$url], $name);
            if ($onStored !== null) {
                $onStored($name);
            }
        }

        return $stored;
    }

    /** @return array<string,string> name => cdn path */
    private function loadManifest(string $version): array
    {
        $key = $this->manifestKey($version);

        return $this->manifestCache[$key] ??= $this->ddragonCache->get(
            $this->cacheKey($key),
            fn (ItemInterface $item): array => $this->readManifest($key),
        );
    }

    /** @return array<string,string> name => cdn path */
    private function readManifest(string $key): array
    {
        try {
            return json_decode($this->ddragonStorage->read($key), true) ?: [];
        } catch (UnableToReadFile) {
            return [];
        }
    }

    /**
     * Merge freshly stored entries into the manifest.
     *
     * Re-reads the manifest straight from object storage — bypassing both the
     * in-request memo and the cross-request pool, either of which would serve a
     * snapshot taken before a concurrent writer's PUT — then writes fresh+additions.
     * This turns the former blind full-file overwrite (where the SSE loader and the
     * kernel.terminate flush raced last-write-wins and silently dropped each other's
     * entries) into a read-merge-write. The window isn't fully closed — read-modify-
     * write stays non-atomic on S3 — but entries no longer vanish between ingests.
     *
     * @param array<string,string> $additions name => cdn path just stored
     */
    private function saveManifest(string $version, array $additions): void
    {
        $key    = $this->manifestKey($version);
        $merged = $additions + $this->readManifest($key);
        $this->manifestCache[$key] = $merged;
        $this->ddragonStorage->write($key, json_encode($merged));
        // Write-through: drop the stale cross-request copy so other workers
        // repopulate from the freshly written manifest on their next read.
        $this->ddragonCache->delete($this->cacheKey($key));
    }

    /** Storage key of this resource's per-version image manifest. */
    private function manifestKey(string $version): string
    {
        return sprintf('manifest/%s/%s.json', $version, $this->type());
    }
}
