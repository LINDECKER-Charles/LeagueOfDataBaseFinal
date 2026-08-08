<?php
declare(strict_types=1);

namespace App\Service\Analytics\Storage;

use App\Service\Analytics\RequestEvent;
use App\Service\Storage\NdjsonDayStore;
use Symfony\Component\DependencyInjection\Attribute\Autowire;

/**
 * Append-only local event log, one NDJSON file per UTC day
 * (var/analytics/events/{Y-m-d}.ndjson). Everything about the day-file format
 * and its atomicity guarantees lives in {@see NdjsonDayStore}; this class only
 * binds the directory and the {@see RequestEvent} write contract.
 *
 * Durability across hosts / `down -v` is the rollup's job ({@see RollupService}),
 * which folds closed days into immutable MinIO aggregates.
 */
final class EventStore extends NdjsonDayStore
{
    private const DIR = 'var/analytics/events';

    public function __construct(
        #[Autowire('%kernel.project_dir%')]
        string $projectDir,
    ) {
        parent::__construct($projectDir, self::DIR);
    }

    /**
     * Best-effort append. Failures are swallowed: this runs at kernel.terminate,
     * after the response is flushed, and must never turn a served page into an
     * error.
     */
    public function append(RequestEvent $event): void
    {
        try {
            $this->appendRow($event->toArray(), $event->at->format('Y-m-d'));
        } catch (\Throwable) {
            // Analytics is best-effort; never propagate into the request lifecycle.
        }
    }
}
