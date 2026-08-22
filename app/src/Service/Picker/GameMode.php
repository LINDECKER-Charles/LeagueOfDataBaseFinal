<?php
declare(strict_types=1);

namespace App\Service\Picker;

use App\Service\Catalog\GameMap;

/**
 * Game modes a build can target, each bound to the Data Dragon map that gates
 * item availability ({@see GameMap}, the one owner of those ids).
 *
 * Deliberately a FIXED subset of the persistent, named modes: Swarm and Brawl
 * are event modes whose MapName is empty in DDragon's own map.json — fine to
 * filter a list on, not a stable identity to build for. The enum values are
 * the persisted `builds.game_mode` strings — never rename them.
 */
enum GameMode: string
{
    case SummonersRift = 'sr';
    case Aram = 'aram';
    case NexusBlitz = 'nexus_blitz';
    case Arena = 'arena';

    public const GameMode DEFAULT = self::SummonersRift;

    /** DDragon map id used against the item.json "maps" availability flags. */
    public function mapId(): string
    {
        return $this->map()->value;
    }

    public function map(): GameMap
    {
        return match ($this) {
            self::SummonersRift => GameMap::SummonersRift,
            self::Aram => GameMap::HowlingAbyss,
            self::NexusBlitz => GameMap::NexusBlitz,
            self::Arena => GameMap::Arena,
        };
    }

    /** Translation key of the player-facing mode label. */
    public function labelKey(): string
    {
        return 'build.mode.'.$this->value;
    }

    /**
     * Lenient form/query parsing: absent or blank means the default mode
     * (legacy builds and JS-less submits), an unknown non-empty value is an
     * explicit user error and yields null so callers can reject it.
     */
    public static function fromForm(?string $value): ?self
    {
        $value = trim((string) $value);

        return $value === '' ? self::DEFAULT : self::tryFrom($value);
    }
}
