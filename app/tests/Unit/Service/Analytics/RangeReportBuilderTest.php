<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

use App\Service\Analytics\AnalyticsAggregator;
use App\Service\Analytics\RangeReportBuilder;
use PHPUnit\Framework\TestCase;

final class RangeReportBuilderTest extends TestCase
{
    private RangeReportBuilder $builder;
    private AnalyticsAggregator $aggregator;

    protected function setUp(): void
    {
        $this->builder = new RangeReportBuilder();
        $this->aggregator = new AnalyticsAggregator();
    }

    /**
     * @param array<string, mixed> $o
     * @return array<string, mixed>
     */
    private function event(array $o): array
    {
        return $o + [
            'bot' => false, 'status' => 200, 'type' => 'champion', 'kind' => 'list',
            'path' => '/champions', 'entity' => '', 'locale' => 'fr', 'lang' => 'fr_FR',
            'browser' => 'Chrome', 'os' => 'Windows', 'device' => 'desktop',
            'refSource' => 'direct', 'refHost' => '', 'at' => '2026-07-14T12:00:00+00:00',
        ];
    }

    /**
     * @return array<string, mixed>
     */
    private function report(): array
    {
        $day1 = $this->aggregator->aggregateDay('2026-07-14', [
            $this->event(['visitor' => 'v1']),
            $this->event(['visitor' => 'v2', 'type' => 'item']),
        ]);
        $day2 = $this->aggregator->aggregateDay('2026-07-15', [
            $this->event(['visitor' => 'v1', 'at' => '2026-07-15T12:00:00+00:00']), // returning
            $this->event(['visitor' => 'v3', 'at' => '2026-07-15T12:00:00+00:00']),
        ]);

        return $this->builder->build([$day1, $day2], '30d');
    }

    public function testTotalsSumViewsAndUnionVisitors(): void
    {
        $report = $this->report();

        self::assertSame(4, $report['totals']['views']);
        self::assertSame(3, $report['totals']['uniqueVisitors']); // v1, v2, v3
    }

    public function testReturningVisitorSeenOnTwoDistinctDays(): void
    {
        $report = $this->report();

        self::assertSame(1, $report['totals']['returningVisitors']); // only v1
    }

    public function testSeriesHasOneEntryPerDay(): void
    {
        $report = $this->report();

        self::assertCount(2, $report['series']);
        self::assertSame('2026-07-14', $report['series'][0]['date']);
        self::assertSame(2, $report['series'][0]['views']);
    }

    public function testRankingsAreSortedDescendingWithPercentages(): void
    {
        $report = $this->report();
        $byType = $report['byType'];

        self::assertSame('champion', $byType[0]['name']); // 3 vs item 1
        self::assertSame(3, $byType[0]['count']);
        self::assertEqualsWithDelta(75.0, $byType[0]['pct'], 0.01);
    }

    public function testHeatmapIsSevenByTwentyFour(): void
    {
        $report = $this->report();

        self::assertCount(7, $report['heatmap']);
        self::assertCount(24, $report['heatmap'][0]);
    }

    public function testMetadataFromToDays(): void
    {
        $report = $this->report();

        self::assertSame('2026-07-14', $report['from']);
        self::assertSame('2026-07-15', $report['to']);
        self::assertSame(2, $report['days']);
    }
}
