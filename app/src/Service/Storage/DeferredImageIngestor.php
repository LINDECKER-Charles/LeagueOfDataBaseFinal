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
 * Deferral is **opt-in and off by default**: image resolution is synchronous
 * unless a caller explicitly runs it inside {@see self::withDeferral()}. Only the
 * list/preview render ({@see \App\Service\API\PaginatesResources::paginate()})
 * tolerates placeholders — detail pages, pickers, search results and the build
 * view all need their images resolved in the same response, so they never opt in
 * and stay synchronous. This makes "resolve now" the safe default; forgetting to
 * opt into deferral only costs a slower cold render, never a broken image.
 *
 * CLI has no request, so {@see self::shouldDefer()} is false there and the warmup
 * command still ingests synchronously in a single pass.
 */
final class DeferredImageIngestor
{
    /** @var list<callable():void> */
    private array $tasks = [];

    /** Whether the current resolution scope opted into deferral ({@see withDeferral}). */
    private bool $deferralAllowed = false;

    public function __construct(private readonly RequestStack $requestStack) {}

    /**
     * Run a resolution routine in a scope where cold images may be deferred to
     * kernel.terminate. Scoped and re-entrant-safe: the previous state is always
     * restored, so nested/sequential renders can't leak the "may defer" flag onto
     * a later synchronous resolution on the same (shared) service instance.
     *
     * @template T
     * @param callable():T $resolve
     * @return T
     */
    public function withDeferral(callable $resolve): mixed
    {
        $previous = $this->deferralAllowed;
        $this->deferralAllowed = true;
        try {
            return $resolve();
        } finally {
            $this->deferralAllowed = $previous;
        }
    }

    /** Defer only when a caller opted in ({@see withDeferral}) AND within an HTTP request (CLI/warmup ingests inline). */
    public function shouldDefer(): bool
    {
        return $this->deferralAllowed && $this->requestStack->getMainRequest() !== null;
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
