<?php
declare(strict_types=1);

namespace App\Service\Analytics;

use League\Flysystem\FileAttributes;
use League\Flysystem\FilesystemOperator;
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
    private const CACHE_TTL = 120;
    private const LARGEST_LIMIT = 15;
    private const SOURCE_EXTS = ['png', 'jpg', 'jpeg', 'gif'];

    public function __construct(
        private readonly FilesystemOperator $ddragonStorage,
        #[Autowire(service: 'ddragon.cache')]
        private readonly CacheInterface $cache,
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

    private function compute(): array
    {
        $acc = $this->newAccumulator();

        try {
            foreach ($this->ddragonStorage->listContents('', FilesystemOperator::LIST_DEEP) as $entry) {
                if ($entry instanceof FileAttributes) {
                    $this->consume($acc, $entry);
                }
            }
            $this->readManifests($acc);
        } catch (\Throwable $e) {
            return ['ok' => false, 'error' => $e->getMessage()] + $this->emptyReport();
        }

        return ['ok' => true, 'error' => null] + $this->assemble($acc);
    }

    private function consume(array &$acc, FileAttributes $file): void
    {
        $path = $file->path();
        $bytes = $file->fileSize() ?? 0;
        $family = strstr($path, '/', true) ?: '/';

        $acc['totalObjects']++;
        $acc['totalBytes'] += $bytes;
        $this->bump($acc['families'], $family, $bytes);
        $this->trackLargest($acc, $path, $bytes);
        $this->trackTimeline($acc, $file, $bytes);

        match ($family) {
            'blobs' => $this->consumeBlob($acc, $path, $bytes),
            'data' => $this->consumeData($acc, $path, $bytes),
            'manifest' => $this->consumeManifest($acc, $path, $bytes),
            default => null,
        };
    }

    private function consumeBlob(array &$acc, string $path, int $bytes): void
    {
        $ext = strtolower(pathinfo($path, PATHINFO_EXTENSION)) ?: 'none';
        $this->bump($acc['blobExt'], $ext, $bytes);

        if ($ext === 'webp') {
            $acc['blobWebp']++;
            $acc['blobWebpBytes'] += $bytes;
        } elseif (in_array($ext, self::SOURCE_EXTS, true)) {
            $acc['blobSources']++;
            $acc['blobSourceBytes'] += $bytes;
        }
    }

    private function consumeData(array &$acc, string $path, int $bytes): void
    {
        // data/{version}/{lang}/{type}.json | data/{version}/{lang}/championDetail/*
        // | data/{version}/cdragon/chromas/*
        $seg = explode('/', $path);
        $version = $seg[1] ?? '?';
        $scope = $seg[2] ?? '?';
        $type = $this->dataType($seg);

        $this->bump($acc['dataVersion'], $version, $bytes);
        $this->bump($acc['dataType'], $type, $bytes);
        if ($scope !== 'cdragon') {
            $this->bump($acc['dataLang'], $scope, $bytes);
            $this->coverage($acc, $version, $scope, $type);
        }
    }

    private function consumeManifest(array &$acc, string $path, int $bytes): void
    {
        $version = explode('/', $path)[1] ?? '?';
        $this->bump($acc['manifestVersion'], $version, $bytes);
        $acc['manifestPaths'][] = $path;
    }

    /**
     * @param array<int, string> $seg
     */
    private function dataType(array $seg): string
    {
        if (($seg[2] ?? '') === 'cdragon') {
            return $seg[3] ?? 'cdragon';
        }
        if (($seg[3] ?? '') !== '' && !str_ends_with($seg[3], '.json')) {
            return $seg[3]; // championDetail/…
        }

        return pathinfo($seg[3] ?? 'unknown', PATHINFO_FILENAME);
    }

    private function coverage(array &$acc, string $version, string $lang, string $type): void
    {
        $row = &$acc['coverage'][$version];
        $row ??= ['langs' => [], 'types' => [], 'objects' => 0];
        $row['langs'][$lang] = true;
        $row['types'][$type] = true;
        $row['objects']++;
    }

    private function readManifests(array &$acc): void
    {
        $logical = 0;
        foreach ($acc['manifestPaths'] as $key) {
            try {
                $decoded = json_decode($this->ddragonStorage->read($key), true);
                $logical += is_array($decoded) ? count($decoded) : 0;
            } catch (\Throwable) {
                // Unreadable manifest — skip, don't fail the whole report.
            }
        }
        $acc['logicalRefs'] = $logical;
    }

    private function trackLargest(array &$acc, string $path, int $bytes): void
    {
        $acc['sizes'][] = ['path' => $path, 'bytes' => $bytes];
    }

    private function trackTimeline(array &$acc, FileAttributes $file, int $bytes): void
    {
        $ts = $file->lastModified();
        $day = $ts !== null ? gmdate('Y-m-d', $ts) : 'unknown';
        $acc['timeline'][$day] ??= ['objects' => 0, 'bytes' => 0];
        $acc['timeline'][$day]['objects']++;
        $acc['timeline'][$day]['bytes'] += $bytes;
    }

    private function assemble(array $acc): array
    {
        return [
            'total' => ['objects' => $acc['totalObjects'], 'bytes' => $acc['totalBytes']],
            'families' => $this->rows($acc['families'], $acc['totalBytes']),
            'blobs' => $this->blobSection($acc),
            'data' => [
                'byVersion' => $this->rows($acc['dataVersion']),
                'byLang' => $this->rows($acc['dataLang']),
                'byType' => $this->rows($acc['dataType']),
            ],
            'manifests' => ['byVersion' => $this->rows($acc['manifestVersion'])],
            'dedup' => $this->dedupSection($acc),
            'largest' => $this->largest($acc['sizes']),
            'timeline' => $this->timeline($acc['timeline']),
            'coverage' => $this->coverageRows($acc['coverage']),
        ];
    }

    private function blobSection(array $acc): array
    {
        $sources = $acc['blobSources'];

        return [
            'byExt' => $this->rows($acc['blobExt']),
            'sources' => $sources,
            'webpSiblings' => $acc['blobWebp'],
            'webpCoverage' => $sources > 0 ? min(1.0, $acc['blobWebp'] / $sources) : 0.0,
            'sourceBytes' => $acc['blobSourceBytes'],
            'webpBytes' => $acc['blobWebpBytes'],
        ];
    }

    private function dedupSection(array $acc): array
    {
        $logical = $acc['logicalRefs'];
        $physical = $acc['families']['blobs']['objects'] ?? 0;
        $avgBlob = $physical > 0 ? (int) (($acc['families']['blobs']['bytes'] ?? 0) / $physical) : 0;

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
            $rows[] = ['date' => $day, 'objects' => $row['objects'], 'bytes' => $row['bytes'], 'cumulativeBytes' => $cumulative];
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
            $rows[] = ['version' => (string) $version, 'langs' => $langs, 'types' => $types, 'objects' => $row['objects']];
        }

        return $rows;
    }

    private function bump(array &$map, string $key, int $bytes): void
    {
        $map[$key] ??= ['objects' => 0, 'bytes' => 0];
        $map[$key]['objects']++;
        $map[$key]['bytes'] += $bytes;
    }

    private function newAccumulator(): array
    {
        return [
            'totalObjects' => 0, 'totalBytes' => 0, 'families' => [],
            'blobExt' => [], 'blobSources' => 0, 'blobWebp' => 0,
            'blobSourceBytes' => 0, 'blobWebpBytes' => 0,
            'dataVersion' => [], 'dataLang' => [], 'dataType' => [],
            'manifestVersion' => [], 'manifestPaths' => [], 'logicalRefs' => 0,
            'sizes' => [], 'timeline' => [], 'coverage' => [],
        ];
    }

    private function emptyReport(): array
    {
        return [
            'total' => ['objects' => 0, 'bytes' => 0], 'families' => [],
            'blobs' => ['byExt' => [], 'sources' => 0, 'webpSiblings' => 0, 'webpCoverage' => 0.0, 'sourceBytes' => 0, 'webpBytes' => 0],
            'data' => ['byVersion' => [], 'byLang' => [], 'byType' => []],
            'manifests' => ['byVersion' => []],
            'dedup' => ['logicalRefs' => 0, 'physicalBlobs' => 0, 'ratio' => 0.0, 'savedBytesApprox' => 0],
            'largest' => [], 'timeline' => [], 'coverage' => [],
        ];
    }
}
