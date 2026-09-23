<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

use App\Service\Analytics\AnalyticsAggregator;
use App\Service\Analytics\AnalyticsReportService;
use App\Service\Analytics\RangeReportBuilder;
use App\Service\Analytics\Storage\DailyAggregateStore;
use App\Service\Analytics\Storage\EventStore;
use League\Flysystem\FilesystemOperator;
use PHPUnit\Framework\MockObject\MockObject;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Cache\Adapter\ArrayAdapter;

/**
 * Memoisation contract of the range report. A window is assembled from one
 * aggregate per day, each a round trip to object storage: closed days are
 * immutable and must survive the expiry of the assembled report, while
 * "Rafraîchir" must still reach through to the source.
 */
final class AnalyticsReportServiceTest extends TestCase
{
    private const RANGE = '7d';
    /** 7d window = 6 closed days + today, which is always aggregated live. */
    private const CLOSED_DAYS = 6;

    public function testAClosedDayIsReadOnceEvenAfterTheRangeReportExpires(): void
    {
        $operator = $this->storageMock();
        $operator->expects(self::exactly(self::CLOSED_DAYS))->method('read');
        $cache = new ArrayAdapter();
        $service = $this->service($operator, $cache);

        $service->report(self::RANGE);
        // Simulates the short expiry of the assembled report: the per-day
        // aggregates it was built from are still valid and must be reused.
        $cache->deleteItem($this->rangeKey());

        self::assertSame(7, $service->report(self::RANGE)['days']);
    }

    public function testRefreshReachesThroughToObjectStorageAgain(): void
    {
        $operator = $this->storageMock();
        $operator->expects(self::exactly(self::CLOSED_DAYS * 2))->method('read');
        $service = $this->service($operator, new ArrayAdapter());

        $service->report(self::RANGE);

        self::assertSame(7, $service->report(self::RANGE, fresh: true)['days']);
    }

    public function testReportCoversTheWholeWindow(): void
    {
        $operator = $this->createStub(FilesystemOperator::class);
        $operator->method('read')->willReturnCallback($this->emptyAggregateOf());

        $report = $this->service($operator, new ArrayAdapter())->report(self::RANGE);

        self::assertSame(self::RANGE, $report['range']);
        self::assertSame(7, $report['days']);
    }

    /** @return MockObject&FilesystemOperator */
    private function storageMock(): MockObject
    {
        $operator = $this->createMock(FilesystemOperator::class);
        $operator->method('read')->willReturnCallback($this->emptyAggregateOf());

        return $operator;
    }

    /** Object storage answering every day with a well-formed empty aggregate. */
    private function emptyAggregateOf(): \Closure
    {
        return static fn (string $key): string => (string) json_encode(
            new AnalyticsAggregator()->aggregateDay(basename($key, '.json'), []),
        );
    }

    private function service(
        FilesystemOperator $operator,
        ArrayAdapter $cache,
    ): AnalyticsReportService {
        return new AnalyticsReportService(
            new EventStore(sys_get_temp_dir() . '/lodb-analytics-report-test'),
            new DailyAggregateStore($operator),
            new AnalyticsAggregator(),
            new RangeReportBuilder(),
            $cache,
        );
    }

    private function rangeKey(): string
    {
        return sprintf('analytics.report.%s.%s', self::RANGE, gmdate('Y-m-d'));
    }
}
