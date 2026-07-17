<?php
declare(strict_types=1);

namespace App\Service\Build;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;

/**
 * Thin I/O shell around {@see BuildStructureValidator}: loads the current
 * (version, lang) catalogs through the resource managers and hands them to the
 * pure core. Data-layer failures (transient upstream) are left to bubble — the
 * controller degrades them into a "catalog unavailable" flash instead of
 * silently accepting an unverified structure.
 */
final class BuildCatalogGate
{
    public function __construct(
        private readonly BuildStructureValidator $validator,
        private readonly ChampionManager $championManager,
        private readonly ItemManager $itemManager,
        private readonly RuneManager $runeManager,
    ) {}

    /**
     * @param array<mixed> $structure decoded structure JSON
     * @return list<string> error codes; empty means valid on this (version, lang)
     */
    public function validate(array $structure, string $version, string $lang): array
    {
        $championIds = array_keys($this->championManager->getData($version, $lang)['data'] ?? []);
        $itemIds = array_keys($this->itemManager->getData($version, $lang)['data'] ?? []);

        return $this->validator->validate(
            $structure,
            $this->runeManager->getData($version, $lang),
            array_map(strval(...), $championIds),
            // PHP recasts numeric JSON keys to int — restore the string ids.
            array_map(strval(...), $itemIds),
        );
    }
}
