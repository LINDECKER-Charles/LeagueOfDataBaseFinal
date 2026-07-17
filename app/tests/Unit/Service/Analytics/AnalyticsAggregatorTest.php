<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

use App\Service\Analytics\AnalyticsAggregator;
use PHPUnit\Framework\TestCase;

final class AnalyticsAggregatorTest extends TestCase
{
    private AnalyticsAggregator $aggregator;

    protected function setUp(): void
    {
        $this->aggregator = new AnalyticsAggregator();
    }

    /**
     * @return list<array<string, mixed>>
     */
    private function events(): array
    {
        return [
            $this->event(['visitor' => 'v1', 'type' => 'champion', 'kind' => 'list', 'path' => '/champions', 'at' => '2026-07-15T10:00:00+00:00', 'browser' => 'Chrome', 'device' => 'desktop', 'refSource' => 'search']),
            $this->event(['visitor' => 'v1', 'type' => 'champion', 'kind' => 'detail', 'entity' => 'Aatrox', 'path' => '/champion/Aatrox', 'at' => '2026-07-15T10:05:00+00:00', 'browser' => 'Chrome', 'device' => 'desktop']),
            $this->event(['visitor' => 'v2', 'type' => 'item', 'kind' => 'list', 'path' => '/objects', 'at' => '2026-07-15T14:00:00+00:00', 'device' => 'mobile', 'country' => 'FR', 'countryName' => 'France']),
            $this->event(['visitor' => 'bot', 'bot' => true, 'at' => '2026-07-15T09:00:00+00:00']),
        ];
    }

    /**
     * @param array<string, mixed> $overrides
     * @return array<string, mixed>
     */
    private function event(array $overrides): array
    {
        return $overrides + [
            'bot' => false, 'status' => 200, 'locale' => 'fr', 'lang' => 'fr_FR',
            'browser' => 'Chrome', 'os' => 'Windows', 'device' => 'desktop',
            'refSource' => 'direct', 'refHost' => '', 'entity' => '', 'route' => 'app_home',
        ];
    }

    public function testBotsAreCountedSeparatelyAndExcludedFromHumanMetrics(): void
    {
        $daily = $this->aggregator->aggregateDay('2026-07-15', $this->events());

        self::assertSame(3, $daily['views']);
        self::assertSame(1, $daily['botViews']);
        self::assertCount(2, $daily['visitors']); // v1, v2 — bot excluded
    }

    public function testContentBreakdowns(): void
    {
        $daily = $this->aggregator->aggregateDay('2026-07-15', $this->events());

        self::assertSame(2, $daily['byType']['champion']);
        self::assertSame(1, $daily['byType']['item']);
        self::assertSame(2, $daily['byKind']['list']);
        self::assertSame(1, $daily['byKind']['detail']);
        self::assertSame(1, $daily['pages']['/champions']);
        self::assertSame(1, $daily['entities']['champion:Aatrox']);
    }

    public function testAudienceBreakdowns(): void
    {
        $daily = $this->aggregator->aggregateDay('2026-07-15', $this->events());

        self::assertSame(2, $daily['device']['desktop']);
        self::assertSame(1, $daily['device']['mobile']);
        self::assertSame(1, $daily['country']['FR']);
        self::assertSame('France', $daily['countryNames']['FR']);
    }

    public function testTimeDistribution(): void
    {
        $daily = $this->aggregator->aggregateDay('2026-07-15', $this->events());

        self::assertSame(2, $daily['byHour'][10]); // two human hits at 10:xx
        self::assertSame(1, $daily['byHour'][14]);
        self::assertSame(3, array_sum($daily['byWeekday']));
        $weekday = (int) (new \DateTimeImmutable('2026-07-15'))->format('N') - 1;
        self::assertArrayHasKey($weekday . ':10', $daily['heatmap']);
    }

    public function testEmptyDayProducesZeroedShape(): void
    {
        $daily = $this->aggregator->aggregateDay('2026-07-15', []);

        self::assertSame(0, $daily['views']);
        self::assertSame([], $daily['visitors']);
        self::assertCount(24, $daily['byHour']);
        self::assertCount(7, $daily['byWeekday']);
    }
}
