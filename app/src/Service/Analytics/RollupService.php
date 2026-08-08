<?php
declare(strict_types=1);

namespace App\Service\Analytics;

use App\Service\Analytics\Storage\DailyAggregateStore;
use App\Service\Analytics\Storage\EventStore;

/**
 * Consolidates closed local NDJSON days into immutable MinIO aggregates
 * (analytics/daily/{date}.json) for durability and cheap historical reads. This
 * is the durability tier: the hot path only appends locally, and a container /
 * `down -v` would otherwise lose recent history — rolling up narrows that window.
 *
 * Idempotent: an already-rolled day is skipped unless forced. Today is never
 * rolled (still open) and never pruned.
 */
final class RollupService
{
    public function __construct(
        private readonly EventStore $events,
        private readonly DailyAggregateStore $dailyStore,
        private readonly AnalyticsAggregator $aggregator,
    ) {}

    /**
     * @return array{rolled: list<string>, skipped: list<string>, pruned: list<string>}
     */
    public function rollup(
        bool $includeToday = false,
        bool $force = false,
        bool $prune = false,
    ): array {
        $today = gmdate('Y-m-d');
        $result = ['rolled' => [], 'skipped' => [], 'pruned' => []];

        foreach ($this->events->days() as $date) {
            $isToday = $date === $today;
            if ($isToday && !$includeToday) {
                continue;
            }
            if (!$force && !$isToday && $this->dailyStore->exists($date)) {
                $result['skipped'][] = $date;
                continue;
            }

            $this->persistAggregate($date);
            $result['rolled'][] = $date;

            if ($prune && !$isToday) {
                $this->events->deleteDay($date);
                $result['pruned'][] = $date;
            }
        }

        return $result;
    }

    /** Folds the day's raw events and writes the immutable aggregate. */
    private function persistAggregate(string $date): void
    {
        $this->dailyStore->write(
            $date,
            $this->aggregator->aggregateDay($date, $this->events->readDay($date)),
        );
    }
}
