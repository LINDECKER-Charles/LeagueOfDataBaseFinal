<?php
declare(strict_types=1);

namespace LoDbParity;

use App\Service\API\Image\ImageStatusInterface;

/**
 * An image of the projection, `{file, status, url}`, as the legacy manifest settles it.
 *
 * manifestStatus() is the read-only look the in-page refresh uses: a manifest entry with a
 * path is `present`, a null entry a definitive `absent`, a missing entry `pending`.
 */
final class ImageStatus
{
    /** Longest key the new manifest stores (ddragon_asset.key). */
    private const MAX_FILE_LENGTH = 255;

    private function __construct()
    {
    }

    /** @return array{file: string, status: string, url: ?string}|null null without a file */
    public static function of(ImageStatusInterface $manager, string $version, mixed $file): ?array
    {
        if (!is_string($file) || trim($file) === '' || strlen($file) > self::MAX_FILE_LENGTH) {
            return null;
        }

        $status = $manager->manifestStatus($version, [$file]);
        if (!array_key_exists($file, $status['images'])) {
            return ['file' => $file, 'status' => 'pending', 'url' => null];
        }

        $path = $status['images'][$file];

        return $path === null
            ? ['file' => $file, 'status' => 'absent', 'url' => null]
            : ['file' => $file, 'status' => 'present', 'url' => '/'.$path];
    }
}
