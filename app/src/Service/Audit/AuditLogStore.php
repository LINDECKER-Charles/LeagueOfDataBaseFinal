<?php
declare(strict_types=1);

namespace App\Service\Audit;

use App\Service\Audit\Model\AuditEvent;
use App\Service\Storage\NdjsonDayStore;
use Symfony\Component\DependencyInjection\Attribute\Autowire;

/**
 * Append-only local audit log, one NDJSON file per UTC day
 * (var/state/audit/events/{Y-m-d}.ndjson). The day-file format and its atomicity
 * guarantees live in {@see NdjsonDayStore}; this class only binds the directory
 * and the {@see AuditEvent} write contract.
 *
 * var/state is the volume-backed path (compose.yaml), so the journal survives the
 * container recreation every deploy performs — a legally retained trail must not
 * depend on a container's lifetime. Cross-host durability and survival across
 * `down -v` remain the rollup's job ({@see AuditRollupService}), which archives
 * closed days verbatim into MinIO — verbatim, not aggregated, because an audit
 * trail must preserve every row.
 */
final class AuditLogStore extends NdjsonDayStore implements AuditDayReader
{
    private const DIR = 'var/state/audit/events';

    public function __construct(
        #[Autowire('%kernel.project_dir%')]
        string $projectDir,
    ) {
        parent::__construct($projectDir, self::DIR);
    }

    /**
     * Append one recorded action. Unlike the analytics store it swallows nothing:
     * a failed write propagates as {@see \App\Service\Storage\NdjsonWriteException}
     * so {@see AuditLogger} can own the "an audit write never breaks its own
     * request — but its loss is never silent" policy in one place.
     */
    public function append(AuditEvent $event): void
    {
        $this->appendRow($event->toArray(), $event->at->format('Y-m-d'));
    }
}
