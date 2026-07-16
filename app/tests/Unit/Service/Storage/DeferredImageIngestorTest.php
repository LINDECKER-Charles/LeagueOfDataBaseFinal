<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Storage;

use App\Service\Storage\DeferredImageIngestor;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

final class DeferredImageIngestorTest extends TestCase
{
    public function testDefersOnlyWithinAnHttpRequest(): void
    {
        $stack = new RequestStack();
        $ingestor = new DeferredImageIngestor($stack);

        self::assertFalse($ingestor->shouldDefer(), 'no request (CLI/warmup) → ingest inline');

        $stack->push(new Request());
        self::assertTrue($ingestor->shouldDefer(), 'within an HTTP request → defer');
    }

    public function testFlushRunsQueuedTasksOnceInOrder(): void
    {
        $ingestor = new DeferredImageIngestor(new RequestStack());
        $calls = [];
        $ingestor->defer(static function () use (&$calls): void { $calls[] = 'a'; });
        $ingestor->defer(static function () use (&$calls): void { $calls[] = 'b'; });

        $ingestor->flush();
        self::assertSame(['a', 'b'], $calls);

        $ingestor->flush(); // consumed — nothing re-runs
        self::assertSame(['a', 'b'], $calls);
    }

    public function testFlushSwallowsFailuresAndContinues(): void
    {
        $ingestor = new DeferredImageIngestor(new RequestStack());
        $ran = false;
        $ingestor->defer(static function (): void { throw new \RuntimeException('boom'); });
        $ingestor->defer(static function () use (&$ran): void { $ran = true; });

        $ingestor->flush(); // must not bubble the failure
        self::assertTrue($ran, 'a failing task must not block the rest');
    }
}
