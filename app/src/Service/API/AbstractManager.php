<?php
declare(strict_types=1);

namespace App\Service\API;

use App\Service\Storage\BlobStore;
use App\Service\Storage\DeferredImageIngestor;
use App\Service\Tools\GoFetcherClient;
use League\Flysystem\FilesystemOperator;
use League\Flysystem\UnableToReadFile;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Contracts\Cache\CacheInterface;
use Symfony\Contracts\Cache\ItemInterface;

/**
 * Base for the DDragon resource managers (champion, item, rune, summoner).
 *
 * Storage model (MinIO / object storage):
 *  - JSON data : data/{version}/{lang}/{type}.json          (logical cache)
 *  - Images    : blobs/{sha256}.{ext}                        (content-addressed, deduped)
 *  - Manifest  : manifest/{version}/{type}.json  name => cdn (image lookup without re-download)
 *
 * All Data Dragon egress goes through the Go fetch gateway ({@see GoFetcherClient}),
 * which fetches image batches in parallel.
 */
abstract class AbstractManager implements WarmableManagerInterface
{
    /** @var array<string,array<mixed>> in-request decoded-data memo, keyed by storage key */
    private array $dataCache = [];

    /** @var array<string,array<string,string>> in-request manifest memo, keyed by storage key */
    private array $manifestCache = [];

    public function __construct(
        protected readonly GoFetcherClient $goFetcher,
        protected readonly FilesystemOperator $ddragonStorage,
        protected readonly BlobStore $blobStore,
        #[Autowire(service: 'ddragon.cache')]
        private readonly CacheInterface $ddragonCache,
        private readonly DeferredImageIngestor $ingestion,
    ) {}

    /** Build the DDragon image URL for a single file name (per-resource). */
    abstract protected function imageUrl(string $version, string $name): string;

    /**
     * Flatten a raw getData() payload into the list of entries to iterate.
     * Shape differs per resource (champion/item/summoner nest under 'data',
     * runes are a top-level list).
     *
     * @param array<mixed> $raw
     * @return array<mixed>
     */
    abstract protected function dataList(array $raw): array;

    /**
     * Map every image of a data slice to a human-readable display name — the
     * single source of "which images this slice needs", shared by getImages,
     * collectPlan and ingest.
     *
     * @param array<mixed> $data
     * @return array<string,string> imageFileName => display name
     */
    abstract protected function imageEntries(array $data): array;

    /**
     * Resolve every image of the resource for a version/language.
     *
     * @param array<mixed> $data
     * @return array<mixed>
     */
    abstract public function getImages(string $version, string $lang, bool $force = false, array $data = []): array;

    public function type(): string
    {
        return static::TYPE;
    }

    /**
     * Fetch the resource's JSON for a version/language, cached in object storage.
     *
     * @return array<mixed>
     */
    public function getData(string $version, string $lang): array
    {
        $key = sprintf('data/%s/%s/%s.json', $version, $lang, static::TYPE);

        // The dataset is immutable per (version, lang): serve it from the
        // in-request memo, then the cross-request cache, before ever touching
        // object storage or the gateway. Avoids a MinIO round-trip + a full
        // json_decode of the whole resource on every page render.
        return $this->dataCache[$key] ??= $this->ddragonCache->get(
            $this->cacheKey($key),
            fn (ItemInterface $item): array => $this->loadOrFetchData($key, $version, $lang),
        );
    }

    /**
     * Read the dataset from object storage, falling back to a one-time fetch
     * through the Go gateway (then persisted) when it is not yet stored.
     *
     * @return array<mixed>
     */
    private function loadOrFetchData(string $key, string $version, string $lang): array
    {
        try {
            return json_decode($this->ddragonStorage->read($key), true) ?? [];
        } catch (UnableToReadFile) {
            // Not in object storage yet → fetch once and persist.
        }

        $data = json_decode($this->goFetcher->fetch($this->jsonUrl($version, $lang)), true) ?? [];
        $this->ddragonStorage->write($key, json_encode($data));

        return $data;
    }

    /** DDragon JSON endpoint for this manager's resource type. */
    protected function jsonUrl(string $version, string $lang): string
    {
        return sprintf(
            'https://ddragon.leagueoflegends.com/cdn/%s/data/%s/%s.json',
            $version,
            $lang,
            static::TYPE
        );
    }

    /**
     * Resolve image file names to public CDN paths for a version.
     *
     * Cache hits come from the per-(version,type) manifest; misses are fetched in a
     * single parallel batch through the gateway, stored content-addressed (dedup),
     * and recorded in the manifest.
     *
     * @param string[] $names
     * @param bool     $allowDefer defer a cold batch to after the response (list pages); false ingests inline
     * @return array<string,string> name => cdn path
     */
    protected function resolveImages(string $version, array $names, bool $force = false, bool $allowDefer = true): array
    {
        $manifest = $this->loadManifest($version);
        $result = [];
        $missing = []; // ddragon url => name

        foreach (array_unique($names) as $name) {
            if (!$force && isset($manifest[$name])) {
                $result[$name] = $manifest[$name];
            } else {
                $missing[$this->imageUrl($version, $name)] = $name;
            }
        }

        if ($missing === []) {
            return $result;
        }

        // Cold on a user request: don't block the render on a multi-second batch
        // fetch. Queue the ingestion for after the response is sent (kernel.terminate)
        // — this page shows placeholders, the next visit is warm. Detail pages
        // (allowDefer=false) and the warmup command (CLI, no request) ingest now.
        if ($allowDefer && $this->ingestion->shouldDefer()) {
            $this->ingestion->defer(fn (): array => $this->ingestMissing($version, $missing));

            return $result;
        }

        return $result + $this->ingestMissing($version, $missing);
    }

    /**
     * Fetch the missing images through the gateway, store them content-addressed
     * (dedup + WebP sibling) and record them in the manifest.
     *
     * @param array<string,string> $missing    ddragon url => name
     * @param (callable(string):void)|null $onStored invoked with each image name as it lands
     * @return array<string,string> name => cdn path (only the ones fetched)
     */
    private function ingestMissing(string $version, array $missing, ?callable $onStored = null): array
    {
        $manifest = $this->loadManifest($version);
        $resolved = [];

        $bytesByUrl = $this->goFetcher->fetchMany(array_keys($missing));
        foreach ($missing as $url => $name) {
            if (!isset($bytesByUrl[$url])) {
                continue;
            }
            $cdn = $this->blobStore->store($bytesByUrl[$url], $name);
            $manifest[$name] = $cdn;
            $resolved[$name] = $cdn;
            if ($onStored !== null) {
                $onStored($name);
            }
        }

        if ($resolved !== []) {
            $this->saveManifest($version, $manifest);
        }

        return $resolved;
    }

    /**
     * Cost of warming the images of a page slice, computed without fetching:
     * the full entry map plus how many of those images are not yet stored.
     * Backs the streaming loader's determinate progress total.
     *
     * @return array{entries: array<string,string>, missing: int}
     */
    public function collectPlan(string $version, string $lang, int $perPage, int $page): array
    {
        $list  = $this->dataList($this->getData($version, $lang));
        $slice = $perPage <= 0
            ? $list
            : $this->splitJson($perPage, $page <= 1 ? 0 : $perPage * ($page - 1), $list);

        $entries  = $this->imageEntries($slice);
        $manifest = $this->loadManifest($version);
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
     * by the streaming loader ({@see \App\Controller\LoaderController}) to warm a
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
     * Ingest images from explicit DDragon URLs whose shape differs from
     * {@see imageUrl()} — champion spell/passive icons ({@code img/spell/…},
     * {@code img/passive/…}), splash art, etc. Reuses the per-type manifest,
     * blob dedup and WebP sibling; synchronous, for detail pages.
     *
     * @param array<string,string> $urlsByName name => ddragon url
     * @return array<string,string> name => cdn path
     */
    protected function resolveExternalImages(string $version, array $urlsByName, bool $force = false): array
    {
        $manifest = $this->loadManifest($version);
        $result = [];
        $missing = []; // ddragon url => name

        foreach ($urlsByName as $name => $url) {
            if (!$force && isset($manifest[$name])) {
                $result[$name] = $manifest[$name];
            } else {
                $missing[$url] = $name;
            }
        }

        if ($missing === []) {
            return $result;
        }

        return $result + $this->ingestMissing($version, $missing);
    }

    /** Resolve a single image (detail pages) — always synchronous. */
    protected function resolveImage(string $version, string $name, bool $force = false): string
    {
        $resolved = $this->resolveImages($version, [$name], $force, allowDefer: false);
        if (!isset($resolved[$name])) {
            throw new \RuntimeException(sprintf('Image indisponible: %s (%s)', $name, static::TYPE));
        }

        return $resolved[$name];
    }

    /**
     * @return array<string,string> name => cdn path
     */
    private function loadManifest(string $version): array
    {
        $key = sprintf('manifest/%s/%s.json', $version, static::TYPE);

        return $this->manifestCache[$key] ??= $this->ddragonCache->get(
            $this->cacheKey($key),
            fn (ItemInterface $item): array => $this->readManifest($key),
        );
    }

    /**
     * @return array<string,string> name => cdn path
     */
    private function readManifest(string $key): array
    {
        try {
            return json_decode($this->ddragonStorage->read($key), true) ?: [];
        } catch (UnableToReadFile) {
            return [];
        }
    }

    /**
     * @param array<string,string> $manifest
     */
    private function saveManifest(string $version, array $manifest): void
    {
        $key = sprintf('manifest/%s/%s.json', $version, static::TYPE);
        $this->manifestCache[$key] = $manifest;
        $this->ddragonStorage->write($key, json_encode($manifest));
        // Write-through: drop the stale cross-request copy so other workers
        // repopulate from the freshly written manifest on their next read.
        $this->ddragonCache->delete($this->cacheKey($key));
    }

    /** PSR-6 safe cache key derived from a storage path ('/' is reserved). */
    private function cacheKey(string $storageKey): string
    {
        return str_replace('/', '.', $storageKey);
    }

    /**
     * @param array<mixed> $json
     * @return array<mixed>
     */
    protected final function splitJson(int $nb, int $start, array $json): array
    {
        return array_slice($json, $start, $nb, true);
    }
}
