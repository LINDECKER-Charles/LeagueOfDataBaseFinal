<?php
declare(strict_types=1);

namespace App\Service\Storage;

use Symfony\Component\HttpFoundation\RequestStack;

/**
 * Collects image-ingestion work to run AFTER the HTTP response is sent
 * (kernel.terminate), so a cold — unwarmed — list render returns immediately with
 * placeholder images instead of blocking on a multi-second DDragon batch fetch.
 * The next visit finds them warm.
 *
 * CLI has no request, so {@see self::shouldDefer()} is false there and the warmup
 * command still ingests synchronously in a single pass.
 */
final class DeferredImageIngestor
{
    /** @var list<callable():void> */
    private array $tasks = [];

    public function __construct(private readonly RequestStack $requestStack) {}

    /** Defer only within an HTTP request; CLI/warmup must ingest synchronously. */
    public function shouldDefer(): bool
    {
        return $this->requestStack->getMainRequest() !== null;
    }

    public function defer(callable $task): void
    {
        $this->tasks[] = $task;
    }

    /** Run every queued task once, swallowing failures (best-effort warming). */
    public function flush(): void
    {
        $tasks = $this->tasks;
        $this->tasks = [];
        foreach ($tasks as $task) {
            try {
                $task();
            } catch (\Throwable) {
                // A failed warm just leaves placeholders for the next visit to retry.
            }
        }
    }
}
