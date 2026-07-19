<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Storage;

use App\Service\Storage\DeferredImageIngestor;
use PHPUnit\Framework\TestCase;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;

final class DeferredImageIngestorTest extends TestCase
{
    public function testDeferralIsOptInAndRequiresAnHttpRequest(): void
    {
        $stack = new RequestStack();
        $ingestor = new DeferredImageIngestor($stack);

        // Safe default: never defer unless explicitly opted in — even under a request.
        self::assertFalse($ingestor->shouldDefer(), 'default (no opt-in) → ingest inline');
        $stack->push(new Request());
        self::assertFalse($ingestor->shouldDefer(), 'HTTP request alone is not enough → still inline');

        // Opt-in only defers when there is also a request to defer to.
        $ingestor->withDeferral(function () use ($ingestor): void {
            self::assertTrue($ingestor->shouldDefer(), 'opt-in within a request → defer');
        });

        // CLI/warmup: opting in without a request still ingests inline.
        $cli = new DeferredImageIngestor(new RequestStack());
        $cli->withDeferral(function () use ($cli): void {
            self::assertFalse($cli->shouldDefer(), 'opt-in without a request (CLI) → inline');
        });
    }

    public function testWithDeferralScopeIsRestoredAfterwards(): void
    {
        $stack = new RequestStack();
        $stack->push(new Request());
        $ingestor = new DeferredImageIngestor($stack);

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
        $ingestor = new DeferredImageIngestor(new RequestStack());
        self::assertSame(42, $ingestor->withDeferral(static fn (): int => 42));
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
