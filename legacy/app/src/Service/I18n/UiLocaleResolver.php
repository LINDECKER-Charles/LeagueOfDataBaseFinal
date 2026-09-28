<?php

declare(strict_types=1);

namespace App\Service\I18n;

/**
 * Maps a Data Dragon content locale (e.g. "fr_FR", "es_MX", "zh_TW") to the
 * application's UI locale used by the Symfony translator, then clamps the result
 * to the set of locales that actually ship a message catalog.
 *
 * Rationale: DDragon exposes ~27 region-specific locales but the interface only
 * needs one catalog per language. Every locale collapses to its 2-letter base
 * language, except Chinese which keeps the Simplified/Traditional distinction.
 */
final class UiLocaleResolver
{
    /**
     * @param string[] $enabledLocales UI locales having a catalog (%kernel.enabled_locales%)
     */
    public function __construct(
        private readonly array $enabledLocales,
    ) {}

    /**
     * Maps a DDragon locale to a UI locale and clamps it to an enabled catalog.
     * Falls back to $fallback (itself expected to be an enabled locale) when the
     * input is null, unmapped, or has no catalog.
     */
    public function resolve(?string $ddragonLocale, string $fallback): string
    {
        if ($ddragonLocale !== null && $ddragonLocale !== '') {
            $ui = $this->toUiLocale($ddragonLocale);
            if (\in_array($ui, $this->enabledLocales, true)) {
                return $ui;
            }
        }

        return $fallback;
    }

    /**
     * Pure DDragon-locale -> UI-locale mapping (no catalog awareness).
     * Chinese keeps its script distinction; every other locale collapses to its
     * 2-letter base language.
     */
    public function toUiLocale(string $ddragonLocale): string
    {
        return match (true) {
            str_starts_with($ddragonLocale, 'zh_TW') => 'zh_Hant',
            str_starts_with($ddragonLocale, 'zh_')   => 'zh_Hans',
            default => strtolower(substr($ddragonLocale, 0, 2)),
        };
    }
}
