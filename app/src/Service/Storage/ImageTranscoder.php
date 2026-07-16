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
    public function isSupported(): bool
    {
        return function_exists('imagewebp') && function_exists('imagecreatefromstring');
    }

    /**
     * @param int      $quality 0-100 (WebP)
     * @param int|null $maxDim  cap the longest edge, keeping aspect ratio; null keeps native size
     * @return string|null WebP bytes, or null if the source can't be transcoded
     */
    public function toWebp(string $bytes, int $quality = 82, ?int $maxDim = null): ?string
    {
        if ($bytes === '' || !$this->isSupported()) {
            return null;
        }

        $src = @imagecreatefromstring($bytes);
        if (!$src instanceof \GdImage) {
            return null; // not a raster GD can decode (e.g. SVG)
        }

        $img = $maxDim !== null ? $this->downscale($src, $maxDim) : $src;

        try {
            imagepalettetotruecolor($img);
            imagealphablending($img, false);
            imagesavealpha($img, true);

            ob_start();
            $ok  = imagewebp($img, null, max(0, min(100, $quality)));
            $out = ob_get_clean();

            return ($ok && is_string($out) && $out !== '') ? $out : null;
        } finally {
            if ($img !== $src) {
                imagedestroy($img);
            }
            imagedestroy($src);
        }
    }

    private function downscale(\GdImage $src, int $maxDim): \GdImage
    {
        $w = imagesx($src);
        $h = imagesy($src);
        if (max($w, $h) <= $maxDim) {
            return $src; // never upscale
        }

        $ratio = $maxDim / max($w, $h);
        $nw = max(1, (int) round($w * $ratio));
        $nh = max(1, (int) round($h * $ratio));

        $dst = imagecreatetruecolor($nw, $nh);
        imagealphablending($dst, false);
        imagesavealpha($dst, true);
        imagefill($dst, 0, 0, imagecolorallocatealpha($dst, 0, 0, 0, 127));
        imagecopyresampled($dst, $src, 0, 0, 0, 0, $nw, $nh, $w, $h);

        return $dst;
    }
}
