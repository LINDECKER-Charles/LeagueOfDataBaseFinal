<?php
declare(strict_types=1);

namespace App\Service\Analytics\Model;

/**
 * Where a page view came from: the bare referring host (null when there is
 * none) and the source it was attributed to by
 * {@see \App\Service\Analytics\RefererClassifier}.
 */
final readonly class RefererOrigin
{
    public function __construct(
        public ?string $host,
        public RefererSource $source,
    ) {}

    /** No usable Referer — a bookmark, a typed URL, or a stripped header. */
    public static function direct(): self
    {
        return new self(null, RefererSource::Direct);
    }
}
