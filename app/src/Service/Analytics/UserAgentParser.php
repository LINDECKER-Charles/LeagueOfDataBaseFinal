<?php
declare(strict_types=1);

namespace App\Service\Analytics;

/**
 * Dependency-free User-Agent classification. A deliberately small heuristic — it
 * only needs to power audience breakdowns (browser / OS / device family / bot),
 * not to be a exhaustive UA database. Ordering matters: more specific tokens are
 * tested before the generic engines they embed (Edg before Chrome, Chrome before
 * Safari, etc.).
 */
final class UserAgentParser
{
    private const BOT_MARKERS = [
        'bot', 'crawl', 'spider', 'slurp', 'bingpreview', 'facebookexternalhit',
        'embedly', 'quora', 'pinterest', 'headless', 'phantom', 'puppeteer',
        'lighthouse', 'python-requests', 'curl', 'wget', 'go-http', 'okhttp',
        'monitor', 'uptime', 'pingdom', 'semrush', 'ahrefs', 'dataprovider',
    ];

    /** Browser needle => label, most specific first. */
    private const BROWSERS = [
        'Edg' => 'Edge', 'OPR' => 'Opera', 'Opera' => 'Opera',
        'SamsungBrowser' => 'Samsung Internet', 'YaBrowser' => 'Yandex',
        'Vivaldi' => 'Vivaldi', 'Brave' => 'Brave', 'Firefox' => 'Firefox',
        'Chrome' => 'Chrome', 'CriOS' => 'Chrome', 'Safari' => 'Safari',
        'MSIE' => 'Internet Explorer', 'Trident' => 'Internet Explorer',
    ];

    /** OS needle => label, most specific first. */
    private const SYSTEMS = [
        'Windows NT' => 'Windows', 'iPhone' => 'iOS', 'iPad' => 'iPadOS',
        'Android' => 'Android', 'CrOS' => 'ChromeOS', 'Mac OS X' => 'macOS',
        'Macintosh' => 'macOS', 'Linux' => 'Linux',
    ];

    /**
     * @return array{browser: string, os: string, device: string, isBot: bool}
     */
    public function parse(?string $ua): array
    {
        $ua = trim((string) $ua);
        if ($ua === '') {
            return ['browser' => 'other', 'os' => 'other', 'device' => 'other', 'isBot' => false];
        }

        if ($this->looksLikeBot($ua)) {
            return ['browser' => 'Bot', 'os' => 'other', 'device' => 'bot', 'isBot' => true];
        }

        return [
            'browser' => $this->match(self::BROWSERS, $ua, 'other'),
            'os' => $this->match(self::SYSTEMS, $ua, 'other'),
            'device' => $this->device($ua),
            'isBot' => false,
        ];
    }

    private function looksLikeBot(string $ua): bool
    {
        $needle = strtolower($ua);
        foreach (self::BOT_MARKERS as $marker) {
            if (str_contains($needle, $marker)) {
                return true;
            }
        }

        return false;
    }

    private function device(string $ua): string
    {
        if (preg_match('/iPad|Tablet|Nexus 7|Nexus 10|Kindle|Silk|PlayBook/i', $ua) === 1) {
            return 'tablet';
        }
        if (str_contains($ua, 'Android') && !str_contains($ua, 'Mobile')) {
            return 'tablet';
        }
        if (preg_match('/Mobi|iPhone|iPod|Windows Phone|IEMobile|BlackBerry/i', $ua) === 1) {
            return 'mobile';
        }

        return 'desktop';
    }

    /**
     * @param array<string, string> $table
     */
    private function match(array $table, string $ua, string $default): string
    {
        foreach ($table as $needle => $label) {
            if (str_contains($ua, $needle)) {
                return $label;
            }
        }

        return $default;
    }
}
