<?php
declare(strict_types=1);

namespace App\Service\API\Champion;

/**
 * The ONE builder of hotlinked champion art URLs (splash / loading / centered).
 * Art is served straight from the Data Dragon CDN, never ingested — a deliberate
 * TTFB choice (see project notes) — so every URL is a pure function of the
 * champion id and the skin number, and every caller (profile banners, list
 * tiles, skin galleries) must go through here to get the casing right.
 */
final class ChampionArt
{
    private const CDN_BASE = 'https://ddragon.leagueoflegends.com/cdn/img/champion';

    /**
     * Riot's INTERNAL champion spelling where it diverges from the public id.
     * Since League of Legends Classic the CDN serves the public spelling
     * (`Fiddlesticks_N.jpg`) with the pre-rework art — and 403s it for every
     * post-rework skin (num ≥ 27) and for the whole `centered/` family — while
     * the internal spelling answers the current art for every kind and skin.
     * The public id must therefore never reach a URL for these champions.
     * One known divergence today; extend the map if Riot adds another.
     */
    private const ART_IDS = ['Fiddlesticks' => 'FiddleSticks'];

    public function url(string $championId, ChampionArtKind $kind, int $skinNum = 0): string
    {
        return sprintf(
            '%s/%s/%s_%d.jpg',
            self::CDN_BASE,
            $kind->value,
            self::ART_IDS[$championId] ?? $championId,
            $skinNum,
        );
    }
}
