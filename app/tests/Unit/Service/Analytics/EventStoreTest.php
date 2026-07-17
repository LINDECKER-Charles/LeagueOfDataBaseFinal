<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics;

use App\Service\Analytics\EventStore;
use App\Service\Analytics\RequestEvent;
use PHPUnit\Framework\TestCase;

final class EventStoreTest extends TestCase
{
    private string $dir;
    private EventStore $store;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir() . '/lodb_events_' . bin2hex(random_bytes(6));
        $this->store = new EventStore($this->dir);
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

    private function event(string $date, string $visitor = 'v1', ?string $entity = null): RequestEvent
    {
        return new RequestEvent(
            at: new \DateTimeImmutable($date . 'T10:00:00', new \DateTimeZone('UTC')),
            route: 'app_champion', path: '/champion/Aatrox', type: 'champion', kind: 'detail',
            entity: $entity, status: 200, version: '15.1.1', lang: 'fr_FR', locale: 'fr',
            ip: '203.0.113.7', visitorId: $visitor, userAgent: 'UA', browser: 'Chrome',
            os: 'Windows', device: 'desktop', isBot: false, refererHost: 'google.com',
            refererSource: 'search', country: 'FR', countryName: 'France',
        );
    }

    public function testAppendThenReadRoundTrip(): void
    {
        $this->store->append($this->event('2026-07-15', 'v1', 'Aatrox'));
        $this->store->append($this->event('2026-07-15', 'v2', 'Ahri'));

        $rows = iterator_to_array($this->store->readDay('2026-07-15'));

        self::assertCount(2, $rows);
        self::assertSame('v1', $rows[0]['visitor']);
        self::assertSame('Aatrox', $rows[0]['entity']);
        self::assertSame('champion', $rows[0]['type']);
        self::assertFalse($rows[0]['bot']);
    }

    public function testEventsArePartitionedByUtcDay(): void
    {
        $this->store->append($this->event('2026-07-15'));
        $this->store->append($this->event('2026-07-16'));

        self::assertCount(1, iterator_to_array($this->store->readDay('2026-07-15')));
        self::assertCount(1, iterator_to_array($this->store->readDay('2026-07-16')));
        self::assertSame(['2026-07-16', '2026-07-15'], $this->store->days()); // newest first
    }

    public function testReadingMissingDayYieldsNothing(): void
    {
        self::assertSame([], iterator_to_array($this->store->readDay('1999-01-01')));
        self::assertFalse($this->store->hasDay('1999-01-01'));
    }

    public function testDeleteDayRemovesTheFile(): void
    {
        $this->store->append($this->event('2026-07-15'));
        self::assertTrue($this->store->hasDay('2026-07-15'));

        $this->store->deleteDay('2026-07-15');

        self::assertFalse($this->store->hasDay('2026-07-15'));
    }
}
