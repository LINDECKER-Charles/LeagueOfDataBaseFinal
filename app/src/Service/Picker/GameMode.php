<?php
declare(strict_types=1);

namespace App\Service\Picker;

/**
 * Game modes a build can target, each bound to the Data Dragon map id that
 * gates item availability in item.json ("maps": {"11": bool, ...}).
 *
 * Deliberately a FIXED list of the persistent, named modes: on current data
 * (16.14.1) item.json also carries map 22 (TFT, zero items) and maps 33/35
 * (Swarm/Brawl — event modes whose MapName is empty in DDragon's own map.json),
 * none of which offer a stable identity worth exposing to players. The enum
 * values are the persisted `builds.game_mode` strings — never rename them.
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
        return match ($this) {
            self::SummonersRift => '11',
            self::Aram => '12',
            self::NexusBlitz => '21',
            self::Arena => '30',
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
