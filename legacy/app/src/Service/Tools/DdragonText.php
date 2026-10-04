<?php
declare(strict_types=1);

namespace App\Service\Tools;

/**
 * Data Dragon copy occasionally leaks raw template tokens the CDN never
 * resolves ("{{ Item_Cooldown }}", "@BaseHeal@"…). They carry no displayable
 * value: one rule strips them for every surface (the `ddragon_text` Twig
 * filter, the JSON-LD descriptions).
 */
final class DdragonText
{
    public static function clean(?string $html): string
    {
        if ($html === null || $html === '') {
            return '';
        }

        $stripped = preg_replace(['/\{\{[^{}]*\}\}/', '/@[\w.]+@/'], '', $html) ?? $html;

        return trim(preg_replace('/[ \t]{2,}/', ' ', $stripped) ?? $stripped);
    }

    /**
     * Display form of a Data Dragon category tag: "CriticalStrike" reads
     * "Critical Strike" — the same split on the card chips and in the list
     * facet, so both agree. Matching keeps using the raw token.
     */
    public static function tagLabel(?string $tag): string
    {
        return preg_replace('/([a-z])([A-Z])/', '$1 $2', (string) $tag) ?? (string) $tag;
    }

    /**
     * A display NAME out of a field Riot sometimes ships as marked-up copy —
     * items 3901-3903 name themselves
     * "<rarityLegendary>Feu à volonté</rarityLegendary><br><subtitleLeft>…".
     * The name proper is what precedes the first <br>; the price subtitle after
     * it is not part of an identity. Plain names pass through untouched.
     */
    public static function plainName(?string $name): string
    {
        if ($name === null || $name === '') {
            return '';
        }
        if (!str_contains($name, '<')) {
            return trim($name);
        }

        $head = preg_split('/<br\s*\/?\s*>/i', $name, 2)[0] ?? $name;

        return trim(strip_tags(self::clean($head)));
    }
}
