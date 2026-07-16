<?php
declare(strict_types=1);

namespace App\Service\Storage;

use League\Flysystem\FileAttributes;
use League\Flysystem\FilesystemOperator;

/**
 * Aggregates object-storage usage (MinIO / S3) by top-level prefix so the admin
 * panel can report how much space each family of resources takes.
 *
 * The DDragon bucket is content-addressed and laid out as `blobs/`, `data/` and
 * `manifest/` (see {@see BlobStore}); the first path segment is used as the family
 * key. Reads are best-effort: if the store is unreachable the report degrades to
 * `ok = false` instead of bubbling a 500 into the panel.
 */
final class StorageUsageService
{
    private const ROOT_LABEL = '/';

    public function __construct(private readonly FilesystemOperator $ddragonStorage) {}

    /**
     * @return array{
     *   ok: bool,
     *   error: string|null,
     *   prefixes: array<string, array{objects: int, bytes: int}>,
     *   total: array{objects: int, bytes: int}
     * }
     */
    public function report(): array
    {
        $prefixes = [];
        $totalObjects = 0;
        $totalBytes = 0;

        try {
            foreach ($this->ddragonStorage->listContents('', FilesystemOperator::LIST_DEEP) as $item) {
                if (!$item instanceof FileAttributes) {
                    continue;
                }

                $bytes = $item->fileSize() ?? 0;
                $family = strstr($item->path(), '/', true) ?: self::ROOT_LABEL;

                $prefixes[$family] ??= ['objects' => 0, 'bytes' => 0];
                $prefixes[$family]['objects']++;
                $prefixes[$family]['bytes'] += $bytes;

                $totalObjects++;
                $totalBytes += $bytes;
            }
        } catch (\Throwable $e) {
            return [
                'ok' => false,
                'error' => $e->getMessage(),
                'prefixes' => [],
                'total' => ['objects' => 0, 'bytes' => 0],
            ];
        }

        uasort($prefixes, static fn (array $a, array $b): int => $b['bytes'] <=> $a['bytes']);

        return [
            'ok' => true,
            'error' => null,
            'prefixes' => $prefixes,
            'total' => ['objects' => $totalObjects, 'bytes' => $totalBytes],
        ];
    }
}
