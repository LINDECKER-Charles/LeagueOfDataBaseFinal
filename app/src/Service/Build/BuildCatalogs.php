<?php
declare(strict_types=1);

namespace App\Service\Build;

/**
 * The three catalogs a build is checked (or forward-ported) against on one
 * (version, lang): rune trees, champion ids, item data. Travels as one value so
 * the shape lives here instead of being re-declared in every signature that
 * passes it around.
 *
 * Champion ids are normalised to strings by {@see of()} — PHP recasts numeric
 * JSON map keys to int, and every consumer used to remember that on its own.
 */
final readonly class BuildCatalogs
{
    /**
     * @param array<mixed>             $runeTrees   raw runesReforged tree list
     * @param list<string>             $championIds e.g. ["Aatrox", ...]
     * @param array<int|string, mixed> $itemData    raw item.json "data" map
     */
    private function __construct(
        public array $runeTrees,
        public array $championIds,
        public array $itemData,
    ) {}

    /**
     * @param array<mixed>             $runeTrees
     * @param list<int|string>         $championIds ids as the data layer keys them
     * @param array<int|string, mixed> $itemData
     */
    public static function of(array $runeTrees, array $championIds, array $itemData): self
    {
        return new self($runeTrees, array_map(strval(...), array_values($championIds)), $itemData);
    }
}
