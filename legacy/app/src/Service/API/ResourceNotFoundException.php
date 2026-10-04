<?php
declare(strict_types=1);

namespace App\Service\API;

/**
 * A lookup key that does not exist in the dataset of the requested
 * (version, lang) — a definitive absence, not a data-layer failure. Detail
 * controllers turn it into a real HTTP 404 so crawlers stop seeing soft-404
 * redirects, while transient errors keep the historical redirect behaviour.
 *
 * Extends RuntimeException on purpose: pre-existing catch sites (search API,
 * home previews) treat it exactly like before.
 */
final class ResourceNotFoundException extends \RuntimeException
{
    public static function forEntry(string $type, string $name): self
    {
        return new self(sprintf('No %s entry "%s" in the requested dataset.', $type, $name));
    }
}
