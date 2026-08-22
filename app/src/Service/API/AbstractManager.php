<?php
declare(strict_types=1);

namespace App\Service\API;

use App\Service\API\Concern\IngestsImages;
use App\Service\API\Concern\PaginatesResources;
use App\Service\API\Concern\ResolvesEntries;
use App\Service\API\Concern\ResolvesImages;
use App\Service\API\Image\ImageStatusInterface;
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
 * This class owns the dataset half of that model (fetch, cache, storage keys);
 * the image halves are composed in: {@see ResolvesImages} (names => cdn paths),
 * {@see IngestsImages} (fetch/store/manifest), plus {@see PaginatesResources}
 * (collection slicing) and {@see ResolvesEntries} (single-entry lookup).
 *
 * All Data Dragon egress goes through the Go fetch gateway ({@see GoFetcherClient}),
 * which fetches image batches in parallel.
 */
abstract class AbstractManager implements WarmableManagerInterface, ImageStatusInterface
{
    use IngestsImages;
    use PaginatesResources;
    use ResolvesEntries;
    use ResolvesImages;

    /** Sole spelling of the CDN host — must stay aligned with the go-fetcher's ALLOWED_HOSTS. */
    protected const DDRAGON_CDN = 'https://ddragon.leagueoflegends.com/cdn';

    /**
     * The only locale guaranteed on every DDragon patch — used as the fallback
     * when a requested language is absent from an old version.
     */
    private const FALLBACK_LANG = 'en_US';

    /** @var array<string,array<mixed>> in-request decoded-data memo, keyed by storage key */
    private array $dataCache = [];

    public function __construct(
        protected readonly GoFetcherClient $goFetcher,
        protected readonly FilesystemOperator $ddragonStorage,
        protected readonly BlobStore $blobStore,
        #[Autowire(service: 'ddragon.cache')]
        private readonly CacheInterface $ddragonCache,
        private readonly DeferredImageIngestor $ingestion,
    ) {}

    /**
     * Fetch the resource's JSON for a version/language, cached in object storage.
     *
     * @return array<mixed>
     */
    public function getData(string $version, string $lang): array
    {
        return $this->dataset(new DatasetRef($version, $lang));
    }

    /**
     * Same payload as {@see getData()}, addressed by the dataset it belongs to —
     * the form every caller below the manager boundary uses.
     *
     * The dataset is immutable per {@see DatasetRef}: serve it from the
     * in-request memo, then the cross-request cache, before ever touching object
     * storage or the gateway. Avoids a MinIO round-trip + a full json_decode of
     * the whole resource on every page render.
     *
     * @return array<mixed>
     */
    protected function dataset(DatasetRef $ref): array
    {
        $key = $this->datasetKey($ref);

        return $this->dataCache[$key] ??= $this->ddragonCache->get(
            $this->cacheKey($key),
            fn (ItemInterface $item): array => $this->loadOrFetchData($ref),
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
    private function loadOrFetchData(DatasetRef $ref): array
    {
        return $this->storedJson($this->datasetKey($ref), function () use ($ref): array {
            try {
                $url = $this->jsonUrl($ref);

                return json_decode($this->goFetcher->fetch($url), true) ?? [];
            } catch (UpstreamNotFoundException) {
                return $ref->lang === self::FALLBACK_LANG
                    ? []
                    : $this->dataset($ref->withLang(self::FALLBACK_LANG));
            }
        });
    }

    /**
     * Read a JSON payload from object storage, fetching it once through the
     * gateway (then persisting it) on a miss — the single spelling of "object
     * storage is the cache, the gateway is the origin".
     *
     * @param callable():array<mixed> $fetch
     * @return array<mixed>
     */
    protected function storedJson(string $key, callable $fetch): array
    {
        try {
            return json_decode($this->ddragonStorage->read($key), true) ?? [];
        } catch (UnableToReadFile) {
            // Not in object storage yet → fetch once and persist.
        }

        $data = $fetch();
        $this->ddragonStorage->write($key, json_encode($data));

        return $data;
    }

    /** DDragon JSON endpoint for this manager's resource type. */
    protected function jsonUrl(DatasetRef $ref): string
    {
        return sprintf(
            '%s/%s/data/%s/%s.json',
            self::DDRAGON_CDN,
            $ref->version,
            $ref->lang,
            $this->type(),
        );
    }

    /**
     * Id-keyed `data` map (never runes' top-level list) — the lookup root of the
     * item recipe/related resolutions.
     *
     * @return array<mixed>
     */
    protected function dataMap(DatasetRef $ref): array
    {
        return $this->dataset($ref)['data'] ?? [];
    }

    /** Storage key of this resource's dataset. */
    private function datasetKey(DatasetRef $ref): string
    {
        return $this->scopedDataKey($ref->version, $ref->lang, $this->type().'.json');
    }

    /**
     * Key of any payload under the `data/` root — the one place that layout is
     * spelled out, so secondary payloads stay consistent with the datasets.
     */
    protected function scopedDataKey(string $version, string ...$segments): string
    {
        return sprintf('data/%s/%s', $version, implode('/', $segments));
    }

    /** PSR-6 safe cache key derived from a storage path ('/' is reserved). */
    private function cacheKey(string $storageKey): string
    {
        return str_replace('/', '.', $storageKey);
    }
}
