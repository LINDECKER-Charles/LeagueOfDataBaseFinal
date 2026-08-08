<?php
declare(strict_types=1);

namespace App\Service\Analytics;

/**
 * Folds raw NDJSON events into a per-day aggregate — the mergeable unit shared by
 * the live reader and the MinIO rollup. Pure and framework-free: it takes decoded
 * event rows and returns plain arrays, so both the rollup command and the report
 * service (over local files) reuse the exact same counting logic.
 *
 * Bots are counted apart (botViews) and excluded from every human breakdown so
 * "most consulted pages" and audience metrics reflect real visitors.
 */
final class AnalyticsAggregator
{
    public const HOURS_PER_DAY = 24;
    public const DAYS_PER_WEEK = 7;

    /**
     * Name => count buckets of a daily aggregate. Single source of truth for the
     * mergeable shape: {@see emptyDaily()} allocates them and
     * {@see RangeReportBuilder::mergeAllMaps()} sums them, so a new bucket can
     * never be persisted yet silently dropped from the report.
     *
     * @var list<string>
     */
    public const COUNTER_BUCKETS = [
        'byType', 'byKind', 'byRoute', 'status', 'pages', 'entities', 'heatmap',
        'locale', 'lang', 'browser', 'os', 'device', 'refSource', 'refHost', 'country',
    ];

    private const HEATMAP_KEY_SEPARATOR = ':';

    /**
     * Audience buckets always counted, with the value standing in for a missing
     * field. Table-driven so a new bucket is one line here instead of one more
     * near-identical statement in {@see foldAudience()}.
     *
     * @var array<string, string>
     */
    private const AUDIENCE_FALLBACKS = [
        'locale' => '?',
        'browser' => 'other',
        'os' => 'other',
        'device' => 'other',
        'refSource' => 'direct',
    ];

    /** Audience buckets counted only when the event carries the field. */
    private const OPTIONAL_AUDIENCE_BUCKETS = ['lang', 'refHost'];

    /**
     * @param iterable<array<string, mixed>> $events
     * @return array<string, mixed> a daily aggregate (see emptyDaily())
     */
    public function aggregateDay(string $date, iterable $events): array
    {
        $daily = $this->emptyDaily($date);
        foreach ($events as $event) {
            $this->fold($daily, $event);
        }
        // Persist the visitor set as a compact list.
        $daily['visitors'] = array_keys($daily['visitors']);

        return $daily;
    }

    /**
     * @param array<string, mixed> $daily
     * @param array<string, mixed> $event
     */
    private function fold(array &$daily, array $event): void
    {
        if (!empty($event['bot'])) {
            $daily['botViews']++;

            return;
        }

        $daily['views']++;
        $daily['visitors'][(string) ($event['visitor'] ?? '')] = true;
        $this->foldContent($daily, $event);
        $this->foldAudience($daily, $event);
        $this->foldTime($daily, (string) ($event['at'] ?? ''));
    }

    /**
     * @param array<string, mixed> $daily
     * @param array<string, mixed> $event
     */
    private function foldContent(array &$daily, array $event): void
    {
        $this->inc($daily['byType'], (string) ($event['type'] ?? '?'));
        $this->inc($daily['byKind'], (string) ($event['kind'] ?? '?'));
        $this->inc($daily['byRoute'], (string) ($event['route'] ?? '?'));
        $this->inc($daily['status'], (string) ($event['status'] ?? '?'));

        $path = (string) ($event['path'] ?? '');
        if ($path !== '') {
            $this->inc($daily['pages'], $path);
        }
        $entity = (string) ($event['entity'] ?? '');
        if ($entity !== '') {
            $this->inc($daily['entities'], ($event['type'] ?? '?') . ':' . $entity);
        }
    }

    /**
     * @param array<string, mixed> $daily
     * @param array<string, mixed> $event
     */
    private function foldAudience(array &$daily, array $event): void
    {
        foreach (self::AUDIENCE_FALLBACKS as $bucket => $fallback) {
            $this->inc($daily[$bucket], (string) ($event[$bucket] ?? $fallback));
        }

        foreach (self::OPTIONAL_AUDIENCE_BUCKETS as $bucket) {
            $value = (string) ($event[$bucket] ?? '');
            if ($value !== '') {
                $this->inc($daily[$bucket], $value);
            }
        }

        $this->foldCountry($daily, $event);
    }

    /**
     * The display name travels with the code so the report can label a country
     * without a lookup table; it is kept apart from the counters because it is a
     * label, not a mergeable count.
     *
     * @param array<string, mixed> $daily
     * @param array<string, mixed> $event
     */
    private function foldCountry(array &$daily, array $event): void
    {
        $country = (string) ($event['country'] ?? '');
        if ($country === '') {
            return;
        }

        $this->inc($daily['country'], $country);
        $daily['countryNames'][$country] = (string) ($event['countryName'] ?? $country);
    }

    /**
     * @param array<string, mixed> $daily
     */
    private function foldTime(array &$daily, string $at): void
    {
        if ($at === '') {
            return;
        }
        try {
            $moment = new \DateTimeImmutable($at);
        } catch (\Throwable) {
            return;
        }
        $hour = (int) $moment->format('G');
        $weekday = (int) $moment->format('N') - 1; // 0 = Monday
        $daily['byHour'][$hour]++;
        $daily['byWeekday'][$weekday]++;
        $this->inc($daily['heatmap'], self::heatmapKey($weekday, $hour));
    }

    /**
     * The heatmap is a flat map keyed by a composite so it stays JSON-friendly
     * and mergeable; encoding and decoding live here so the convention has a
     * single owner.
     */
    public static function heatmapKey(int $weekday, int $hour): string
    {
        return $weekday . self::HEATMAP_KEY_SEPARATOR . $hour;
    }

    /** @return array{0: int, 1: int} weekday (0 = Monday), hour */
    public static function parseHeatmapKey(string $key): array
    {
        $parts = array_pad(explode(self::HEATMAP_KEY_SEPARATOR, $key, 2), 2, '0');

        return [(int) $parts[0], (int) $parts[1]];
    }

    /**
     * @param array<string, int> $map
     */
    private function inc(array &$map, string $key, int $by = 1): void
    {
        $map[$key] = ($map[$key] ?? 0) + $by;
    }

    /**
     * @return array<string, mixed>
     */
    private function emptyDaily(string $date): array
    {
        return [
            'date' => $date,
            'views' => 0, 'botViews' => 0,
            'visitors' => [],
            'byHour' => array_fill(0, self::HOURS_PER_DAY, 0),
            'byWeekday' => array_fill(0, self::DAYS_PER_WEEK, 0),
            'countryNames' => [],
        ] + array_fill_keys(self::COUNTER_BUCKETS, []);
    }
}
