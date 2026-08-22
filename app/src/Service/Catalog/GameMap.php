<?php
declare(strict_types=1);

namespace App\Service\Catalog;

/**
 * The Data Dragon map ids that gate item availability in item.json
 * (`"maps": {"11": bool, …}`), as far as they carry a player-facing identity.
 * Single owner of that knowledge: the build modes ({@see \App\Service\Picker\GameMode})
 * and the item list's map facet both read it here.
 *
 * Deliberately absent: 22 (TFT — never true on any item) and 453, the LoL
 * Classic Rift. The latter is an EDITION, not a map: 265 current items carry the
 * flag too, so it would contradict {@see \App\Service\API\Edition\ItemEditionRule}
 * (id-based, the only reliable discriminator — see project notes).
 */
enum GameMap: string
{
    case SummonersRift = '11';
    case HowlingAbyss = '12';
    case NexusBlitz = '21';
    case Arena = '30';
    case Swarm = '33';
    case Brawl = '35';

    /** Translation key of the player-facing map label. */
    public function labelKey(): string
    {
        return 'map.'.$this->value;
    }

    /**
     * The maps an item.json entry is available on.
     *
     * @param array<mixed> $entry
     * @return list<self>
     */
    public static function availableOn(array $entry): array
    {
        $maps = $entry['maps'] ?? [];
        if (!\is_array($maps)) {
            return [];
        }

        return array_values(array_filter(
            self::cases(),
            static fn (self $map): bool => ($maps[$map->value] ?? false) === true,
        ));
    }
}
