<?php
declare(strict_types=1);

namespace App\Service\Analytics\Storage;

use League\Flysystem\FileAttributes;

/**
 * Fold of one deep listing of the object bucket: every tally the storage report
 * needs, accumulated in a single pass. Knowing how a content-addressed key is
 * read (families, blob extensions, data coordinates) lives here; turning the
 * tallies into a report is the caller's job
 * ({@see \App\Service\Analytics\Storage\StorageAnalyticsService}).
 *
 * The tallies are public and mutable on purpose: this is a scratch accumulator
 * bound to one listing, not a value object — accessors would only restate the
 * field names.
 */
final class BucketScan
{
    // Top-level storage families (first path segment of every content-addressed key).
    public const FAMILY_BLOBS = 'blobs';
    public const FAMILY_DATA = 'data';
    public const FAMILY_MANIFEST = 'manifest';

    /** Ingested image formats; each may own a sibling WebP under the same digest. */
    private const SOURCE_EXTS = ['png', 'jpg', 'jpeg', 'gif'];
    /** data/{version}/cdragon/… is language-agnostic, so it is out of the coverage matrix. */
    private const SCOPE_CDRAGON = 'cdragon';
    private const UNKNOWN = '?';

    public int $totalObjects = 0;
    public int $totalBytes = 0;
    public int $blobSources = 0;
    public int $blobWebp = 0;
    public int $blobSourceBytes = 0;
    public int $blobWebpBytes = 0;
    public int $logicalRefs = 0;
    public int $manifestBytes = 0;
    public int $manifestLastModified = 0;

    /** @var array<string, array{objects: int, bytes: int}> */
    public array $families = [];
    /** @var array<string, array{objects: int, bytes: int}> */
    public array $blobExt = [];
    /** @var array<string, array{objects: int, bytes: int}> */
    public array $dataVersion = [];
    /** @var array<string, array{objects: int, bytes: int}> */
    public array $dataLang = [];
    /** @var array<string, array{objects: int, bytes: int}> */
    public array $dataType = [];
    /** @var array<string, array{objects: int, bytes: int}> */
    public array $manifestVersion = [];
    /** @var array<string, array{objects: int, bytes: int}> */
    public array $timeline = [];
    /** @var list<string> */
    public array $manifestPaths = [];
    /** @var list<array{path: string, bytes: int}> */
    public array $sizes = [];
    /**
     * Version => the langs and types seen for it, for the completeness matrix.
     *
     * @var array<string, array{
     *     langs: array<string, true>, types: array<string, true>, objects: int
     * }>
     */
    public array $coverage = [];

    public function consume(FileAttributes $file): void
    {
        $path = $file->path();
        $bytes = $file->fileSize() ?? 0;
        $at = $file->lastModified();
        $family = strstr($path, '/', true) ?: '/';

        $this->totalObjects++;
        $this->totalBytes += $bytes;
        $this->bump($this->families, $family, $bytes);
        $this->sizes[] = ['path' => $path, 'bytes' => $bytes];
        $this->trackTimeline($at, $bytes);

        match ($family) {
            self::FAMILY_BLOBS => $this->consumeBlob($path, $bytes),
            self::FAMILY_DATA => $this->consumeData($path, $bytes),
            self::FAMILY_MANIFEST => $this->consumeManifest($path, $bytes, $at),
            default => null,
        };
    }

    /**
     * Identity of the manifest set as this listing saw it: count, cumulated
     * weight and freshest write. Reading every manifest is the costly half of the
     * storage report (one object read each), so its result is memoised under this
     * key — any manifest written since bumps the timestamp and the weight, so a
     * stale value cannot survive an ingest.
     */
    public function manifestFingerprint(): string
    {
        return sprintf(
            '%d-%d-%d',
            count($this->manifestPaths),
            $this->manifestBytes,
            $this->manifestLastModified,
        );
    }

    private function consumeBlob(string $path, int $bytes): void
    {
        $ext = strtolower(pathinfo($path, PATHINFO_EXTENSION)) ?: 'none';
        $this->bump($this->blobExt, $ext, $bytes);

        if ($ext === 'webp') {
            $this->blobWebp++;
            $this->blobWebpBytes += $bytes;
        } elseif (in_array($ext, self::SOURCE_EXTS, true)) {
            $this->blobSources++;
            $this->blobSourceBytes += $bytes;
        }
    }

    private function consumeData(string $path, int $bytes): void
    {
        // data/{version}/{lang}/{type}.json | data/{version}/{lang}/championDetail/*
        // | data/{version}/cdragon/chromas/*
        $seg = explode('/', $path);
        $version = $seg[1] ?? self::UNKNOWN;
        $scope = $seg[2] ?? self::UNKNOWN;
        $type = $this->dataTypeOf($seg);

        $this->bump($this->dataVersion, $version, $bytes);
        $this->bump($this->dataType, $type, $bytes);
        if ($scope !== self::SCOPE_CDRAGON) {
            $this->bump($this->dataLang, $scope, $bytes);
            $this->markCoverage($version, $scope, $type);
        }
    }

    private function consumeManifest(string $path, int $bytes, ?int $at): void
    {
        $version = explode('/', $path)[1] ?? self::UNKNOWN;
        $this->bump($this->manifestVersion, $version, $bytes);
        $this->manifestPaths[] = $path;
        $this->manifestBytes += $bytes;
        $this->manifestLastModified = max($this->manifestLastModified, $at ?? 0);
    }

    /**
     * @param array<int, string> $seg
     */
    private function dataTypeOf(array $seg): string
    {
        if (($seg[2] ?? '') === self::SCOPE_CDRAGON) {
            return $seg[3] ?? self::SCOPE_CDRAGON;
        }
        if (($seg[3] ?? '') !== '' && !str_ends_with($seg[3], '.json')) {
            return $seg[3]; // championDetail/…
        }

        return pathinfo($seg[3] ?? 'unknown', PATHINFO_FILENAME);
    }

    private function markCoverage(string $version, string $lang, string $type): void
    {
        $row = &$this->coverage[$version];
        $row ??= ['langs' => [], 'types' => [], 'objects' => 0];
        $row['langs'][$lang] = true;
        $row['types'][$type] = true;
        $row['objects']++;
    }

    private function trackTimeline(?int $at, int $bytes): void
    {
        $day = $at !== null ? gmdate('Y-m-d', $at) : 'unknown';
        $this->timeline[$day] ??= ['objects' => 0, 'bytes' => 0];
        $this->timeline[$day]['objects']++;
        $this->timeline[$day]['bytes'] += $bytes;
    }

    /**
     * @param array<string, array{objects: int, bytes: int}> $map
     */
    private function bump(array &$map, string $key, int $bytes): void
    {
        $map[$key] ??= ['objects' => 0, 'bytes' => 0];
        $map[$key]['objects']++;
        $map[$key]['bytes'] += $bytes;
    }
}
