<?php
declare(strict_types=1);

namespace App\Service\Seo;

/**
 * Maps a UI locale (framework.enabled_locales) to the territory-qualified code
 * Open Graph expects in og:locale. The territory choice mirrors the dominant
 * Data Dragon region for each language (e.g. pt → pt_BR, matching DDragon's
 * only Portuguese locale).
 */
final class OgLocale
{
    public const DEFAULT = 'en_US';

    private const MAP = [
        'ar' => 'ar_AE',
        'cs' => 'cs_CZ',
        'de' => 'de_DE',
        'el' => 'el_GR',
        'en' => 'en_US',
        'es' => 'es_ES',
        'fr' => 'fr_FR',
        'hu' => 'hu_HU',
        'id' => 'id_ID',
        'it' => 'it_IT',
        'ja' => 'ja_JP',
        'ko' => 'ko_KR',
        'pl' => 'pl_PL',
        'pt' => 'pt_BR',
        'ro' => 'ro_RO',
        'ru' => 'ru_RU',
        'th' => 'th_TH',
        'tr' => 'tr_TR',
        'vi' => 'vi_VN',
        'zh_Hans' => 'zh_CN',
        'zh_Hant' => 'zh_TW',
    ];

    public function fromUiLocale(string $uiLocale): string
    {
        return self::MAP[$uiLocale] ?? self::DEFAULT;
    }
}
