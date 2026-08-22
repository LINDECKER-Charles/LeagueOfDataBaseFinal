<?php
declare(strict_types=1);

namespace App\Twig\Codex;

use App\Service\API\Image\ImageStatusResolver;
use App\Service\Storage\WebpSibling;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * Image plumbing the templates must not re-derive: the WebP twin of a blob
 * ({@see WebpSibling}, shared with the in-page refresh endpoint) and the
 * batch size that endpoint accepts, so the poller reads it off the page
 * instead of carrying its own copy of the constant.
 */
final class ImageExtension extends AbstractExtension
{
    public function getFunctions(): array
    {
        return [
            new TwigFunction('webp_sibling', WebpSibling::of(...)),
            new TwigFunction('image_status_batch', $this->batch(...)),
        ];
    }

    public function batch(): int
    {
        return ImageStatusResolver::MAX_NAMES_PER_CALL;
    }
}
