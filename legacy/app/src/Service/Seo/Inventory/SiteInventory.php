<?php
declare(strict_types=1);

namespace App\Service\Seo\Inventory;

use App\Service\API\AbstractManager;
use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;
use App\Service\Client\VersionManager;

/**
 * Reads a factual {@see InventorySnapshot} off the live datasets: the current
 * patch and how many entries each section holds.
 *
 * These are the numbers that make the project describable ("X champions and Y
 * items for patch Z"), so one reader backs both the /about/data page and
 * /llms.txt rather than each restating — and drifting from — the other.
 */
final class SiteInventory
{
    public function __construct(
        private readonly VersionManager $versionManager,
        private readonly DatasetIndexReader $datasets,
        private readonly ChampionManager $champions,
        private readonly ItemManager $items,
        private readonly RuneManager $runes,
        private readonly SummonerManager $summoners,
    ) {}

    /**
     * Snapshot for a patch (defaults to the latest).
     *
     * A section whose dataset cannot be read reports null, not 0: the page must
     * survive a transient upstream outage (this is descriptive copy, not a
     * contract), but it must not answer "0 champions" — that would state a
     * falsehood instead of admitting the number is unknown.
     */
    public function snapshot(?string $version = null): InventorySnapshot
    {
        $version ??= $this->latestVersion();

        return new InventorySnapshot(
            version:   $version,
            champions: $this->count($this->champions, $version),
            items:     $this->count($this->items, $version),
            runes:     $this->count($this->runes, $version),
            summoners: $this->count($this->summoners, $version),
        );
    }

    /** Latest Data Dragon patch, or '' when the upstream version list is unavailable. */
    public function latestVersion(): string
    {
        return $this->versionManager->latestVersion();
    }

    /** Entry count, or null when the dataset is unreadable (unknown ≠ empty). */
    private function count(AbstractManager $manager, string $version): ?int
    {
        $index = $this->datasets->read($manager, $version);

        return $index === null ? null : \count($index);
    }
}
