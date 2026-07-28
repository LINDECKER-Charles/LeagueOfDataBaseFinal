<?php
declare(strict_types=1);

namespace App\Service\Seo;

/**
 * What the site publishes at one point in time: the live patch and the entry
 * count of each section.
 *
 * Counts are nullable, and null means "could not be read" — never 0. Rendering
 * a transient upstream outage as "0 champions" would state a falsehood on the
 * very pages that claim to be factual, and would freeze a failure as data (the
 * same reason the resource managers let transient errors bubble up).
 *
 * Passing this value around (rather than the service that computes it) keeps
 * the consumers — the /about pages and the /llms.txt builder — pure functions
 * of their input, and makes them testable without reaching for the datasets.
 */
final readonly class InventorySnapshot
{
    public function __construct(
        /** Data Dragon patch the counts describe; '' when the version list is unavailable. */
        public string $version,
        public ?int $champions,
        public ?int $items,
        public ?int $runes,
        public ?int $summoners,
    ) {}

    /** True when every section reported a count — i.e. the snapshot is fully known. */
    public function isComplete(): bool
    {
        return $this->version !== ''
            && $this->champions !== null
            && $this->items !== null
            && $this->runes !== null
            && $this->summoners !== null;
    }
}
