<?php
declare(strict_types=1);

namespace App\Service\Analytics;

use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Contracts\Cache\CacheInterface;
use Symfony\Contracts\Cache\ItemInterface;

/**
 * Assembles the traffic + audience report for a time range. Each day is sourced
 * either from its immutable MinIO aggregate (rolled-up past days) or, when that
 * doesn't exist yet, by aggregating the local NDJSON on the fly — so the panel
 * is correct whether or not the rollup has ever run (today is always live).
 */
final class AnalyticsReportService
{
    /** Range token => number of days (null = all history). */
    private const RANGES = ['7d' => 7, '30d' => 30, '90d' => 90, 'all' => null];
    private const DEFAULT_RANGE = '30d';
    private const CACHE_TTL = 45;

    public function __construct(
        private readonly EventStore $events,
        private readonly DailyAggregateStore $dailyStore,
        private readonly AnalyticsAggregator $aggregator,
        private readonly RangeReportBuilder $builder,
        #[Autowire(service: 'ddragon.cache')]
        private readonly CacheInterface $cache,
    ) {}

    /**
     * @return list<string>
     */
    public function ranges(): array
    {
        return array_keys(self::RANGES);
    }

    public function normalizeRange(string $range): string
    {
        return isset(self::RANGES[$range]) ? $range : self::DEFAULT_RANGE;
    }

    /**
     * @return array<string, mixed>
     */
    public function report(string $range, bool $fresh = false): array
    {
        $range = $this->normalizeRange($range);
        $key = sprintf('analytics.report.%s.%s', $range, gmdate('Y-m-d'));
        if ($fresh) {
            $this->cache->delete($key);
        }

        return $this->cache->get($key, function (ItemInterface $item) use ($range): array {
            $item->expiresAfter(self::CACHE_TTL);
            $today = new \DateTimeImmutable('today', new \DateTimeZone('UTC'));
            $dates = $this->windowDates($range, $today);
            $dailies = array_map(fn (string $date): array => $this->dailyFor($date, $today), $dates);

            return $this->builder->build($dailies, $range);
        });
    }

    /**
     * @return list<string> ascending Y-m-d dates covering the range
     */
    private function windowDates(string $range, \DateTimeImmutable $today): array
    {
        $span = self::RANGES[$range];
        $start = $span !== null
            ? $today->modify(sprintf('-%d days', $span - 1))
            : $this->earliestDate($today);

        $dates = [];
        for ($d = $start; $d <= $today; $d = $d->modify('+1 day')) {
            $dates[] = $d->format('Y-m-d');
        }

        return $dates ?: [$today->format('Y-m-d')];
    }

    private function earliestDate(\DateTimeImmutable $today): \DateTimeImmutable
    {
        $candidates = array_merge($this->events->days(), $this->dailyStore->dates());
        if ($candidates === []) {
            return $today;
        }
        sort($candidates);

        try {
            return new \DateTimeImmutable($candidates[0], new \DateTimeZone('UTC'));
        } catch (\Throwable) {
            return $today;
        }
    }

    /**
     * @return array<string, mixed>
     */
    private function dailyFor(string $date, \DateTimeImmutable $today): array
    {
        $isPast = $date < $today->format('Y-m-d');
        if ($isPast) {
            $rolled = $this->dailyStore->read($date);
            if ($rolled !== null) {
                return $rolled;
            }
        }

        return $this->aggregator->aggregateDay($date, $this->events->readDay($date));
    }
}
