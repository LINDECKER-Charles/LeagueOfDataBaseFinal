<?php
declare(strict_types=1);

namespace App\Service\Storage;

/**
 * The ONE owner of "which blob has a WebP twin". {@see BlobStore} writes a
 * `.webp` next to every raster it stores, but only PNG sources are guaranteed
 * to transcode (SVG never does), so the modern candidate is derived for `.png`
 * paths only — anything else would point the browser at a file that was never
 * written, a silent 404 inside the `<picture>`.
 */
final class WebpSibling
{
    private const SOURCE_EXTENSION = '.png';
    private const SIBLING_EXTENSION = '.webp';

    /** Sibling path in the same form as the input (leading slash kept), or null. */
    public static function of(string $path): ?string
    {
        if (!str_ends_with($path, self::SOURCE_EXTENSION)) {
            return null;
        }

        return substr($path, 0, -\strlen(self::SOURCE_EXTENSION)).self::SIBLING_EXTENSION;
    }
}
