<?php
declare(strict_types=1);

namespace App\Service\API;

use App\Service\Storage\BlobStore;
use App\Service\Storage\DeferredImageIngestor;
use App\Service\Tools\GoFetcherClient;
use App\Service\Tools\UpstreamNotFoundException;
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
    /**
     * Ingest images in small batches rather than one blocking call. Two reasons:
     *  - the SSE loader gets a stored-name event per batch as it lands, instead of
     *    silence until the whole set is fetched (otherwise the bar sits at 0%);
     *  - each batch is merged into the manifest against fresh storage state, which
     *    bounds the read-modify-write race window to a single batch.
     */
    private const INGEST_CHUNK_SIZE = 12;

    /** Locale served on the 397 versions — used as the fallback when a requested language is absent. */
    private const FALLBACK_LANG = 'en_US';

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
            fn (ItemInterface $item): array => $this->loadOrFetchData($version, $lang),
        );
    }

    /**
     * Read the dataset from object storage, falling back to a one-time fetch
     * through the Go gateway (then persisted) when it is not yet stored.
     *
     * A definitive upstream absence (403/404) is not an error: either the
     * requested *language* does not exist for this version — Data Dragon's
     * back-catalogue carries fewer locales the older the patch — in which case
     * we serve {@see self::FALLBACK_LANG}; or the *resource* predates the version
     * (e.g. runesReforged before 7.22), which yields an empty dataset. Both
     * outcomes are persisted so we never re-hit the CDN for an immutable "absent".
     * Transient failures (5xx/timeout) are intentionally left to bubble up, so a
     * flaky upstream is never frozen as empty.
     *
     * @return array<mixed>
     */
    private function loadOrFetchData(string $version, string $lang): array
    {
        $key = sprintf('data/%s/%s/%s.json', $version, $lang, static::TYPE);

        try {
            return json_decode($this->ddragonStorage->read($key), true) ?? [];
        } catch (UnableToReadFile) {
            // Not in object storage yet → fetch once and persist.
        }

        try {
            $data = json_decode($this->goFetcher->fetch($this->jsonUrl($version, $lang)), true) ?? [];
        } catch (UpstreamNotFoundException) {
            $data = $lang === self::FALLBACK_LANG ? [] : $this->getData($version, self::FALLBACK_LANG);
        }

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
     * Processed in {@see self::INGEST_CHUNK_SIZE}-sized batches: without $onStored
     * (page render, CLI warmup) the outcome is identical to a single pass; with it,
     * each batch reports its stored names as it lands so the loader progresses
     * throughout the network phase instead of only at the end.
     *
     * @param array<string,string> $missing    ddragon url => name
     * @param (callable(string):void)|null $onStored invoked with each image name as it lands
     * @return array<string,string> name => cdn path (only the ones fetched)
     */
    private function ingestMissing(string $version, array $missing, ?callable $onStored = null): array
    {
        $resolved = [];

        foreach (array_chunk($missing, self::INGEST_CHUNK_SIZE, true) as $chunk) {
            $stored     = [];
            $bytesByUrl = $this->goFetcher->fetchMany(array_keys($chunk));
            foreach ($chunk as $url => $name) {
                if (!isset($bytesByUrl[$url])) {
                    continue;
                }
                $stored[$name]   = $this->blobStore->store($bytesByUrl[$url], $name);
                $resolved[$name] = $stored[$name];
                if ($onStored !== null) {
                    $onStored($name);
                }
            }

            // Persist per batch so progress is durable and the manifest merge
            // (see saveManifest) happens against the freshest storage state.
            if ($stored !== []) {
                $this->saveManifest($version, $stored);
            }
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
     * Pagination générique (tranche de liste + images + méta) partagée par les
     * quatre ressources. Seuls varient la racine paginée ({@see paginationCollection} :
     * map `['data']` pour champion/item/summoner, liste top-level pour les runes) et
     * le plafond par page ({@see perPageCap}). La tranche préserve les clés d'origine
     * (id de ressource) — {@see splitJson} slice en mode préservation de clés.
     *
     * @return array<string,mixed> {<type>s, images, meta}
     */
    public function paginate(string $version, string $lang, int $nb = 1, int $numPage = 1): array
    {
        $json = $this->paginationCollection($this->getData($version, $lang));

        $ttSum = count($json);
        if ($nb === 0 || $nb > $ttSum) {
            $nb = min($this->perPageCap(), $ttSum);
        }
        $ttPage = (int) ceil($ttSum / max(1, $nb));
        if ($numPage > $ttPage) {
            $numPage = 1;
        }

        $json = $numPage <= 1
            ? $this->splitJson($nb, 0, $json)
            : $this->splitJson($nb, $nb * ($numPage - 1), $json);

        $images = $this->getImages($version, $lang, false, $json);

        return [
            static::TYPE.'s' => $json,
            'images' => $images,
            'meta' => [
                'currentPage' => $numPage,
                'nombrePage' => $ttPage,
                'itemPerPage' => $nb,
                'totalItem' => $ttSum,
                'type' => static::TYPE,
            ],
        ];
    }

    /**
     * Racine du dataset à paginer. Par défaut la map `['data']` (clé = id de
     * ressource) ; surchargée par les ressources dont le JSON est une liste
     * top-level (runes).
     *
     * @param array<mixed> $raw
     * @return array<mixed>
     */
    protected function paginationCollection(array $raw): array
    {
        return $raw['data'] ?? [];
    }

    /**
     * Ordered route-id => display-name index of the whole collection — backs the
     * previous/next navigation on detail pages without resolving any image.
     * Order is the collection's own (the same one the list pages render).
     *
     * @return array<string,string>
     */
    public function listIndex(string $version, string $lang): array
    {
        $index = [];
        foreach ($this->paginationCollection($this->getData($version, $lang)) as $key => $entry) {
            if (\is_array($entry)) {
                $index[$this->entryRouteId($entry, (string) $key)] = (string) ($entry['name'] ?? $key);
            }
        }

        return $index;
    }

    /** Identifier used by the detail route for one collection entry (map key by default). */
    protected function entryRouteId(array $entry, string $storageKey): string
    {
        return (string) ($entry['id'] ?? $storageKey);
    }

    /** Plafond d'entrées par page quand l'appelant ne borne pas explicitement. */
    protected function perPageCap(): int
    {
        return 20;
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
        $key    = sprintf('manifest/%s/%s.json', $version, static::TYPE);
        $merged = $additions + $this->readManifest($key);
        $this->manifestCache[$key] = $merged;
        $this->ddragonStorage->write($key, json_encode($merged));
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
