<?php
declare(strict_types=1);

namespace App\Service\API\Edition;

/**
 * Edition rule for summoner spells: a classic-era spell is playable in Data
 * Dragon's JADE mode only, and carries a "_Jade" suffixed id
 * (SummonerFlash_Jade, key 74, is the classic twin of SummonerFlash, key 4).
 * The `modes` list is authoritative; the suffix is only used to derive the twin.
 */
final class SummonerEditionRule
{
    public const CLASSIC_MODE = 'JADE';

    private const CLASSIC_SUFFIX = '_Jade';

    /** @param array<string, mixed> $entry a summoner.json spell node */
    public static function of(array $entry): Edition
    {
        return \in_array(self::CLASSIC_MODE, (array) ($entry['modes'] ?? []), true)
            ? Edition::Classic
            : Edition::Modern;
    }

    /** Id the other edition's twin would carry — the caller checks the dataset. */
    public static function counterpartId(string $id): string
    {
        return str_ends_with($id, self::CLASSIC_SUFFIX)
            ? substr($id, 0, -\strlen(self::CLASSIC_SUFFIX))
            : $id.self::CLASSIC_SUFFIX;
    }
}
