<?php
declare(strict_types=1);

namespace App\Service\Audit;

use App\Service\Audit\Model\AuditEvent;
use App\Service\Storage\NdjsonDayStore;
use Symfony\Component\DependencyInjection\Attribute\Autowire;

/**
 * Append-only local audit log, one NDJSON file per UTC day
 * (var/audit/events/{Y-m-d}.ndjson). The day-file format and its atomicity
 * guarantees live in {@see NdjsonDayStore}; this class only binds the directory
 * and the {@see AuditEvent} write contract.
 *
 * Cross-host durability and survival across `down -v` is the rollup's job
 * ({@see AuditRollupService}), which archives closed days verbatim into MinIO —
 * verbatim, not aggregated, because an audit trail must preserve every row.
 */
final class AuditLogStore extends NdjsonDayStore implements AuditDayReader
{
    private const DIR = 'var/audit/events';

    public function __construct(
        #[Autowire('%kernel.project_dir%')]
        string $projectDir,
    ) {
        parent::__construct($projectDir, self::DIR);
    }

    /**
     * Append one recorded action. Unlike the analytics store this raises nothing
     * of its own and swallows nothing extra: {@see AuditLogger} owns the "an
     * audit write never breaks its own request" policy in one place.
     */
    public function append(AuditEvent $event): void
    {
        $this->appendRow($event->toArray(), $event->at->format('Y-m-d'));
    }
}
