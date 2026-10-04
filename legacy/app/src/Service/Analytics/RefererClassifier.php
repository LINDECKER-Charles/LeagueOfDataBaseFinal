<?php
declare(strict_types=1);

namespace App\Service\Analytics;

use App\Service\Analytics\Model\RefererOrigin;
use App\Service\Analytics\Model\RefererSource;

/**
 * Classifies an inbound Referer into a {@see RefererSource} and extracts its
 * bare host. Pure and dependency free; the app host is passed in so an internal
 * navigation isn't miscounted as external referral traffic.
 */
final class RefererClassifier
{
    private const SEARCH_HOSTS = [
        'google.', 'bing.com', 'duckduckgo.com', 'yahoo.', 'yandex.',
        'baidu.com', 'ecosia.org', 'qwant.com', 'startpage.com', 'brave.com',
    ];

    private const SOCIAL_HOSTS = [
        'facebook.com', 'fb.com', 'twitter.com', 'x.com', 't.co', 'reddit.com',
        'youtube.com', 'youtu.be', 'instagram.com', 'tiktok.com', 'linkedin.com',
        'discord.com', 'discord.gg', 'pinterest.', 'twitch.tv', 'mastodon.',
    ];

    public function classify(?string $referer, string $appHost): RefererOrigin
    {
        $referer = trim((string) $referer);
        if ($referer === '') {
            return RefererOrigin::direct();
        }

        $host = strtolower((string) parse_url($referer, PHP_URL_HOST));
        if ($host === '') {
            return RefererOrigin::direct();
        }

        return new RefererOrigin($host, $this->source($host, strtolower($appHost)));
    }

    private function source(string $host, string $appHost): RefererSource
    {
        if ($appHost !== '' && ($host === $appHost || str_ends_with($host, '.' . $appHost))) {
            return RefererSource::Internal;
        }
        if ($this->matches($host, self::SEARCH_HOSTS)) {
            return RefererSource::Search;
        }
        if ($this->matches($host, self::SOCIAL_HOSTS)) {
            return RefererSource::Social;
        }

        return RefererSource::External;
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
