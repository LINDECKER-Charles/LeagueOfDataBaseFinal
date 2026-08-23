<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Storage;

use App\Service\Storage\DeferredImageIngestor;
use App\Tests\Unit\Support\RecordingLogger;
use PHPUnit\Framework\TestCase;
use Psr\Log\LogLevel;
use Psr\Log\NullLogger;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

final class DeferredImageIngestorTest extends TestCase
{
    public function testDeferralIsOptInAndRequiresAnHttpRequest(): void
    {
        $stack = new RequestStack();
        $ingestor = new DeferredImageIngestor($stack, new NullLogger());

        // Safe default: never defer unless explicitly opted in — even under a request.
        self::assertFalse($ingestor->shouldDefer(), 'default (no opt-in) → ingest inline');
        $stack->push(new Request());
        self::assertFalse(
            $ingestor->shouldDefer(),
            'HTTP request alone is not enough → still inline',
        );

        // Opt-in only defers when there is also a request to defer to.
        $ingestor->withDeferral(function () use ($ingestor): void {
            self::assertTrue($ingestor->shouldDefer(), 'opt-in within a request → defer');
        });

        // CLI/warmup: opting in without a request still ingests inline.
        $cli = new DeferredImageIngestor(new RequestStack(), new NullLogger());
        $cli->withDeferral(function () use ($cli): void {
            self::assertFalse($cli->shouldDefer(), 'opt-in without a request (CLI) → inline');
        });
    }

    public function testWithDeferralScopeIsRestoredAfterwards(): void
    {
        $stack = new RequestStack();
        $stack->push(new Request());
        $ingestor = new DeferredImageIngestor($stack, new NullLogger());

        $ingestor->withDeferral(static function (): void {});
        self::assertFalse($ingestor->shouldDefer(), 'the opt-in must not leak past its scope');

        // Nested scopes restore the outer state, not a hard reset.
        $ingestor->withDeferral(function () use ($ingestor): void {
            $ingestor->withDeferral(static function (): void {});
            self::assertTrue($ingestor->shouldDefer(), 'inner scope end restores the outer opt-in');
        });
    }

    public function testWithDeferralReturnsTheClosureResult(): void
    {
        $ingestor = new DeferredImageIngestor(new RequestStack(), new NullLogger());
        self::assertSame(42, $ingestor->withDeferral(static fn (): int => 42));
    }

    public function testFlushRunsQueuedTasksOnceInOrder(): void
    {
        $ingestor = new DeferredImageIngestor(new RequestStack(), new NullLogger());
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
        $ingestor = new DeferredImageIngestor(new RequestStack(), new NullLogger());
        $ran = false;
        $ingestor->defer(static function (): void { throw new \RuntimeException('boom'); });
        $ingestor->defer(static function () use (&$ran): void { $ran = true; });

        $ingestor->flush(); // must not bubble the failure
        self::assertTrue($ran, 'a failing task must not block the rest');
    }

    /**
     * This runs on kernel.terminate, past the flushed response: it was the one
     * failure path nothing could ever observe. Still swallowed — the assertion
     * above stays true — but no longer silent.
     */
    public function testAFailedWarmIsReportedWithoutBreakingTheFlush(): void
    {
        $logger = new RecordingLogger();
        $ingestor = new DeferredImageIngestor(new RequestStack(), $logger);
        $boom = new \RuntimeException('minio refused the write');
        $ingestor->defer(static function () use ($boom): void { throw $boom; });

        $ingestor->flush();

        $record = $logger->only('ingest.deferred_task.failed');

        self::assertSame(LogLevel::WARNING, $record['level']);
        self::assertSame($boom, $record['context']['exception']);
    }

    /** One record per failing task, and nothing at all when they all succeed. */
    public function testSuccessfulWarmsStaySilent(): void
    {
        $logger = new RecordingLogger();
        $ingestor = new DeferredImageIngestor(new RequestStack(), $logger);
        $ingestor->defer(static function (): void {});
        $ingestor->defer(static function (): void { throw new \RuntimeException('boom'); });

        $ingestor->flush();

        self::assertSame(['ingest.deferred_task.failed'], $logger->keys());
    }
}
