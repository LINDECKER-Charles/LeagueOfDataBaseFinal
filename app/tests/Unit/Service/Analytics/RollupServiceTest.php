<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

use App\Service\Analytics\AnalyticsAggregator;
use App\Service\Analytics\DailyAggregateStore;
use App\Service\Analytics\EventStore;
use App\Service\Analytics\RequestEvent;
use App\Service\Analytics\RollupService;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\TestCase;

final class RollupServiceTest extends TestCase
{
    private string $dir;
    private EventStore $events;
    private DailyAggregateStore $dailyStore;
    private RollupService $rollup;
    private string $today;
    private string $yesterday;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir() . '/lodb_rollup_' . bin2hex(random_bytes(6));
        $this->events = new EventStore($this->dir);
        $this->dailyStore = new DailyAggregateStore(new Filesystem(new LocalFilesystemAdapter($this->dir . '/minio')));
        $this->rollup = new RollupService($this->events, $this->dailyStore, new AnalyticsAggregator());
        $this->today = gmdate('Y-m-d');
        $this->yesterday = gmdate('Y-m-d', time() - 86400);
    }

    protected function tearDown(): void
    {
        if (!is_dir($this->dir)) {
            return;
        }
        $it = new \RecursiveIteratorIterator(
            new \RecursiveDirectoryIterator($this->dir, \FilesystemIterator::SKIP_DOTS),
            \RecursiveIteratorIterator::CHILD_FIRST,
        );
        foreach ($it as $f) {
            $f->isDir() ? rmdir($f->getPathname()) : unlink($f->getPathname());
        }
        rmdir($this->dir);
    }

    private function append(string $date, string $visitor): void
    {
        $this->events->append(new RequestEvent(
            at: new \DateTimeImmutable($date . 'T10:00:00', new \DateTimeZone('UTC')),
            route: 'app_home', path: '/', type: 'home', kind: 'home', entity: null,
            status: 200, version: null, lang: null, locale: 'fr', ip: '203.0.113.1',
            visitorId: $visitor, userAgent: 'UA', browser: 'Chrome', os: 'Windows',
            device: 'desktop', isBot: false, refererHost: null, refererSource: 'direct',
            country: null, countryName: null,
        ));
    }

    public function testClosedDayIsRolledUpToDurableStore(): void
    {
        $this->append($this->yesterday, 'v1');
        $this->append($this->yesterday, 'v2');

        $result = $this->rollup->rollup(includeToday: false);

        self::assertSame([$this->yesterday], $result['rolled']);
        self::assertTrue($this->dailyStore->exists($this->yesterday));
        self::assertSame(2, $this->dailyStore->read($this->yesterday)['views']);
    }

    public function testTodayIsSkippedUnlessIncluded(): void
    {
        $this->append($this->today, 'v3');

        $result = $this->rollup->rollup(includeToday: false);
        self::assertSame([], $result['rolled']);
        self::assertFalse($this->dailyStore->exists($this->today));

        $withToday = $this->rollup->rollup(includeToday: true);
        self::assertContains($this->today, $withToday['rolled']);
    }

    public function testAlreadyRolledDayIsSkippedWhenNotForced(): void
    {
        $this->append($this->yesterday, 'v1');
        $this->rollup->rollup();

        $second = $this->rollup->rollup();

        self::assertSame([], $second['rolled']);
        self::assertSame([$this->yesterday], $second['skipped']);
    }

    public function testPruneRemovesLocalNdjsonForRolledClosedDays(): void
    {
        $this->append($this->yesterday, 'v1');

        $result = $this->rollup->rollup(prune: true);

        self::assertSame([$this->yesterday], $result['pruned']);
        self::assertFalse($this->events->hasDay($this->yesterday));
        self::assertTrue($this->dailyStore->exists($this->yesterday)); // durable copy survives
    }
}
