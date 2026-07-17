<?php
declare(strict_types=1);

namespace App\Service\Analytics;

/**
 * Classifies an inbound Referer into a traffic source (direct / internal /
 * search / social / external) and extracts its bare host. Pure and dependency
 * free; the app host is passed in so an internal navigation isn't miscounted as
 * external referral traffic.
 */
final class RefererClassifier
{
    public const DIRECT = 'direct';
    public const INTERNAL = 'internal';
    public const SEARCH = 'search';
    public const SOCIAL = 'social';
    public const EXTERNAL = 'external';

    private const SEARCH_HOSTS = [
        'google.', 'bing.com', 'duckduckgo.com', 'yahoo.', 'yandex.',
        'baidu.com', 'ecosia.org', 'qwant.com', 'startpage.com', 'brave.com',
    ];

    private const SOCIAL_HOSTS = [
        'facebook.com', 'fb.com', 'twitter.com', 'x.com', 't.co', 'reddit.com',
        'youtube.com', 'youtu.be', 'instagram.com', 'tiktok.com', 'linkedin.com',
        'discord.com', 'discord.gg', 'pinterest.', 'twitch.tv', 'mastodon.',
    ];

    /**
     * @return array{host: string|null, source: string}
     */
    public function classify(?string $referer, string $appHost): array
    {
        $referer = trim((string) $referer);
        if ($referer === '') {
            return ['host' => null, 'source' => self::DIRECT];
        }

        $host = strtolower((string) parse_url($referer, PHP_URL_HOST));
        if ($host === '') {
            return ['host' => null, 'source' => self::DIRECT];
        }

        return ['host' => $host, 'source' => $this->source($host, strtolower($appHost))];
    }

    private function source(string $host, string $appHost): string
    {
        if ($appHost !== '' && ($host === $appHost || str_ends_with($host, '.' . $appHost))) {
            return self::INTERNAL;
        }
        if ($this->matches($host, self::SEARCH_HOSTS)) {
            return self::SEARCH;
        }
        if ($this->matches($host, self::SOCIAL_HOSTS)) {
            return self::SOCIAL;
        }

        return self::EXTERNAL;
    }

    /**
     * @param list<string> $needles
     */
    private function matches(string $host, array $needles): bool
    {
        foreach ($needles as $needle) {
            if (str_contains($host, $needle)) {
                return true;
            }
        }

        return false;
    }
}
