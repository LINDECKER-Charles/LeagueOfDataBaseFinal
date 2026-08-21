<?php
declare(strict_types=1);

namespace App\Service\API\Edition;

/**
 * Edition rule for items: the classic-era catalogue is the range of six-digit
 * ids Riot prefixed with "77" (771004 is the classic twin of 1004).
 *
 * Deliberately NOT derived from item.json's "maps" flags — they are incoherent
 * for the classic range (most are flagged on map 12 / ARAM, one on map 11, a
 * few on map 453 only) while every current item is flagged on 453 too. The id
 * prefix is the one signal Riot applies consistently, the same way Arena
 * variants carry a "22" prefix.
 */
final class ItemEditionRule
{
    /** Data Dragon map id of LoL Classic's "Classic Rift". */
    public const CLASSIC_RIFT_MAP = '453';

    private const CLASSIC_ID = '/^77(\d{4})$/';

    /** Ids a classic twin can be derived from — the current 4-digit catalogue. */
    private const TWINNABLE_MODERN_ID = '/^\d{4}$/';

    private const CLASSIC_PREFIX = '77';

    public static function of(string $id): Edition
    {
        return preg_match(self::CLASSIC_ID, $id) === 1 ? Edition::Classic : Edition::Modern;
    }

    /**
     * Map ids an item may honestly claim to be playable on. The raw flags are
     * only trustworthy outside the classic range: every current item is flagged
     * on 453 without existing there, and most classic items are flagged on
     * current maps (ARAM, one even on the Rift) without existing in this game.
     * So: a classic item belongs to the Classic Rift, full stop; a current item
     * keeps its flags minus 453.
     *
     * @param array<string, mixed> $maps item.json "maps" flags (id => bool)
     * @return list<string>
     */
    public static function claimableMapIds(string $id, array $maps): array
    {
        if (self::of($id) === Edition::Classic) {
            return [self::CLASSIC_RIFT_MAP];
        }

        return array_values(array_filter(
            array_map(strval(...), array_keys(array_filter($maps))),
            static fn (string $mapId): bool => $mapId !== self::CLASSIC_RIFT_MAP,
        ));
    }

    /**
     * Display label that stays unambiguous in a flat text list (flash messages):
     * the classic twin shares its name with the current item, so it carries its
     * id — the one token the player can match against the item page.
     */
    public static function qualifiedName(string $id, string $name): string
    {
        return self::of($id) === Edition::Classic ? sprintf('%s [%s]', $name, $id) : $name;
    }

    /**
     * Id the other edition's twin would carry, if it exists — the caller checks
     * the dataset. Null when the id cannot have a twin (Arena / event ranges).
     */
    public static function counterpartId(string $id): ?string
    {
        if (preg_match(self::CLASSIC_ID, $id, $match) === 1) {
            return $match[1];
        }

        return preg_match(self::TWINNABLE_MODERN_ID, $id) === 1
            ? self::CLASSIC_PREFIX.$id
            : null;
    }
}
