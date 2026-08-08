<?php
declare(strict_types=1);

namespace App\Service\Storage;

/**
 * Transcodes raster image bytes (PNG/JPEG/GIF) to WebP via GD, preserving alpha.
 *
 * Returns null whenever the source can't be transcoded — GD/WebP missing, or the
 * bytes aren't a raster GD can decode (e.g. SVG). Callers then keep serving the
 * original, so the image pipeline degrades gracefully on any environment.
 */
final class ImageTranscoder
{
    /**
     * Visually lossless for the flat, small DDragon sprites while roughly halving
     * their weight. Fixed, not a parameter: the pipeline has a single use case.
     */
    private const WEBP_QUALITY = 82;

    public function isSupported(): bool
    {
        return function_exists('imagewebp') && function_exists('imagecreatefromstring');
    }

    /**
     * Blobs are stored at their native DDragon dimensions — no resizing step, so
     * the transcode is a pure format change.
     *
     * @return string|null WebP bytes, or null if the source can't be transcoded
     */
    public function toWebp(string $bytes): ?string
    {
        if ($bytes === '' || !$this->isSupported()) {
            return null;
        }

        $image = @imagecreatefromstring($bytes);
        if (!$image instanceof \GdImage) {
            return null; // not a raster GD can decode (e.g. SVG)
        }

        try {
            imagepalettetotruecolor($image);
            imagealphablending($image, false);
            imagesavealpha($image, true);

            ob_start();
            $isEncoded = imagewebp($image, null, self::WEBP_QUALITY);
            $encoded = ob_get_clean();

            return ($isEncoded && is_string($encoded) && $encoded !== '') ? $encoded : null;
        } finally {
            imagedestroy($image);
        }
    }
}
