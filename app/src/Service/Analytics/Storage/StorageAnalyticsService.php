<?php
declare(strict_types=1);

namespace App\Service\Analytics\Storage;

use League\Flysystem\FileAttributes;
use League\Flysystem\FilesystemOperator;
use Psr\Cache\CacheItemPoolInterface;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Contracts\Cache\CacheInterface;
use Symfony\Contracts\Cache\ItemInterface;

/**
 * Detailed object-storage analytics for the admin panel. A single deep listing
 * of the content-addressed bucket (blobs/ data/ manifest/ + the analytics/
 * rollups) plus a bounded read of the manifests yields: per-family weight, blob
 * breakdown by extension, WebP coverage, the content-addressed dedup ratio
 * (logical image references vs physical blobs), per-version/lang/type data
 * weight, an ingestion timeline, the largest objects, and a version×lang
 * completeness matrix.
 *
 * Reads are best-effort (degrades to ok=false, never a 500) and memoised in
 * ddragon.cache — a full listing is O(objects), too costly to run per panel load.
 */
final class StorageAnalyticsService
{
    private const CACHE_KEY = 'analytics.storage.report';
    /** Content-keyed, so it outlives the report it feeds — see countLogicalRefs(). */
    private const REFS_CACHE_PREFIX = 'analytics.storage.refs.';
    private const CACHE_TTL = 600;
    private const REFS_CACHE_TTL = 86400;
    private const LARGEST_LIMIT = 15;

    public function __construct(
        private readonly FilesystemOperator $ddragonStorage,
        #[Autowire(service: 'ddragon.cache')]
        private readonly CacheInterface&CacheItemPoolInterface $cache,
    ) {}

    public function report(bool $fresh = false): array
    {
        if ($fresh) {
            $this->cache->delete(self::CACHE_KEY);
        }

        return $this->cache->get(self::CACHE_KEY, function (ItemInterface $item): array {
            $item->expiresAfter(self::CACHE_TTL);

            return $this->compute();
        });
    }

    /**
     * Bucket totals *if a report is already memoised*, never at the price of a
     * deep listing. The health probe wants a cheap fact next to its liveness
     * check, not the O(objects) report — see {@see \App\Service\Admin\ServiceHealthProbe}.
     *
     * @return array{objects: int, bytes: int}|null
     */
    public function cachedTotals(): ?array
    {
        $item = $this->cache->getItem(self::CACHE_KEY);
        if (!$item->isHit()) {
            return null;
        }
        $report = $item->get();
        if (!is_array($report) || ($report['ok'] ?? false) !== true) {
            return null;
        }

        return [
            'objects' => (int) $report['total']['objects'],
            'bytes' => (int) $report['total']['bytes'],
        ];
    }

    private function compute(): array
    {
        $scan = new BucketScan();

        try {
            foreach ($this->ddragonStorage->listContents(
                '',
                FilesystemOperator::LIST_DEEP
            ) as $entry) {
                if ($entry instanceof FileAttributes) {
                    $scan->consume($entry);
                }
            }
            $scan->logicalRefs = $this->countLogicalRefs($scan);
        } catch (\Throwable $e) {
            // Assembled from a *fresh* scan, not the partially filled one: half a
            // listing must not be presented as a complete report. Going through
            // assemble() keeps the degraded payload structurally identical to the
            // nominal one — one declaration of the shape.
            return [
                'ok' => false,
                'error' => $e->getMessage(),
            ] + $this->assemble(new BucketScan());
        }

        return ['ok' => true, 'error' => null] + $this->assemble($scan);
    }

    /**
     * Image references declared across every manifest — the logical side of the
     * dedup ratio, against which the physical blob count is compared.
     *
     * One object read per manifest: the dominant cost of the report, and pure
     * function of manifest content. Memoised under the scan's fingerprint, so a
     * recompute (`?refresh=1` included) re-reads them only once an ingest has
     * actually touched a manifest.
     */
    private function countLogicalRefs(BucketScan $scan): int
    {
        return $this->cache->get(
            self::REFS_CACHE_PREFIX . $scan->manifestFingerprint(),
            function (ItemInterface $item) use ($scan): int {
                $item->expiresAfter(self::REFS_CACHE_TTL);

                return $this->readLogicalRefs($scan->manifestPaths);
            },
        );
    }

    /**
     * @param list<string> $manifestPaths
     */
    private function readLogicalRefs(array $manifestPaths): int
    {
        $logical = 0;
        foreach ($manifestPaths as $key) {
            try {
                $decoded = json_decode($this->ddragonStorage->read($key), true);
                $logical += is_array($decoded) ? count($decoded) : 0;
            } catch (\Throwable) {
                // Unreadable manifest — skip, don't fail the whole report.
            }
        }

        return $logical;
    }

    private function assemble(BucketScan $scan): array
    {
        return [
            'total' => ['objects' => $scan->totalObjects, 'bytes' => $scan->totalBytes],
            'families' => $this->rows($scan->families, $scan->totalBytes),
            'blobs' => $this->blobSection($scan),
            'data' => [
                'byVersion' => $this->rows($scan->dataVersion),
                'byLang' => $this->rows($scan->dataLang),
                'byType' => $this->rows($scan->dataType),
            ],
            'manifests' => ['byVersion' => $this->rows($scan->manifestVersion)],
            'dedup' => $this->dedupSection($scan),
            'largest' => $this->largest($scan->sizes),
            'timeline' => $this->timeline($scan->timeline),
            'coverage' => $this->coverageRows($scan->coverage),
        ];
    }

    private function blobSection(BucketScan $scan): array
    {
        $sources = $scan->blobSources;

        return [
            'byExt' => $this->rows($scan->blobExt),
            'sources' => $sources,
            'webpSiblings' => $scan->blobWebp,
            'webpCoverage' => $sources > 0 ? min(1.0, $scan->blobWebp / $sources) : 0.0,
            'sourceBytes' => $scan->blobSourceBytes,
            'webpBytes' => $scan->blobWebpBytes,
        ];
    }

    private function dedupSection(BucketScan $scan): array
    {
        $blobs = $scan->families[BucketScan::FAMILY_BLOBS] ?? ['objects' => 0, 'bytes' => 0];
        $physical = $blobs['objects'];
        $logical = $scan->logicalRefs;
        $avgBlob = $physical > 0 ? (int) ($blobs['bytes'] / $physical) : 0;

        return [
            'logicalRefs' => $logical,
            'physicalBlobs' => $physical,
            'ratio' => $physical > 0 ? $logical / $physical : 0.0,
            'savedBytesApprox' => max(0, $logical - $physical) * $avgBlob,
        ];
    }

    /**
     * @param array<string, array{objects:int, bytes:int}> $map
     * @return list<array{name:string, objects:int, bytes:int, pct:float}>
     */
    private function rows(array $map, ?int $totalBytes = null): array
    {
        uasort($map, static fn (array $a, array $b): int => $b['bytes'] <=> $a['bytes']);
        $rows = [];
        foreach ($map as $name => $row) {
            $rows[] = [
                'name' => (string) $name,
                'objects' => $row['objects'],
                'bytes' => $row['bytes'],
                'pct' => $totalBytes > 0 ? $row['bytes'] / $totalBytes * 100 : 0.0,
            ];
        }

        return $rows;
    }

    /**
     * @param list<array{path:string, bytes:int}> $sizes
     */
    private function largest(array $sizes): array
    {
        usort($sizes, static fn (array $a, array $b): int => $b['bytes'] <=> $a['bytes']);

        return array_slice($sizes, 0, self::LARGEST_LIMIT);
    }

    private function timeline(array $timeline): array
    {
        ksort($timeline);
        $rows = [];
        $cumulative = 0;
        foreach ($timeline as $day => $row) {
            $cumulative += $row['bytes'];
            $rows[] = [
                'date' => $day,
                'objects' => $row['objects'],
                'bytes' => $row['bytes'],
                'cumulativeBytes' => $cumulative,
            ];
        }

        return $rows;
    }

    private function coverageRows(array $coverage): array
    {
        krsort($coverage);
        $rows = [];
        foreach ($coverage as $version => $row) {
            $langs = array_keys($row['langs']);
            $types = array_keys($row['types']);
            sort($langs);
            sort($types);
            $rows[] = [
                'version' => (string) $version,
                'langs' => $langs,
                'types' => $types,
                'objects' => $row['objects'],
            ];
        }

        return $rows;
    }
}
