<?php
declare(strict_types=1);

namespace App\Service\Storage;

use League\Flysystem\FilesystemOperator;

/**
 * Content-addressed image store on top of the object storage (MinIO).
 *
 * Every image is keyed by the SHA-256 of its bytes, so identical content is
 * stored exactly once regardless of version or file name — deduplication is O(1)
 * and inherent, replacing the previous O(versions × size) byte-scan + fragile
 * hard-link scheme.
 */
final class BlobStore
{
    private const PREFIX = 'blobs';

    public function __construct(private readonly FilesystemOperator $ddragonStorage) {}

    /**
     * Store the bytes once and return the public CDN path
     * (e.g. "cdn/blobs/<sha256>.png"). Templates prepend "/" and nginx maps
     * "/cdn/" onto the MinIO bucket.
     *
     * @param string $sourceName original file name, used only to derive the extension
     */
    public function store(string $bytes, string $sourceName): string
    {
        $key = $this->keyFor($bytes, $sourceName);

        if (!$this->ddragonStorage->fileExists($key)) {
            $this->ddragonStorage->write($key, $bytes);
        }

        return 'cdn/'.$key;
    }

    /**
     * Deterministic content-addressed key: blobs/<sha256>.<ext>.
     */
    public function keyFor(string $bytes, string $sourceName): string
    {
        $ext = strtolower(pathinfo($sourceName, PATHINFO_EXTENSION)) ?: 'png';

        return sprintf('%s/%s.%s', self::PREFIX, hash('sha256', $bytes), $ext);
    }
}
