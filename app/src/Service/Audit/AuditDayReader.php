<?php
declare(strict_types=1);

namespace App\Service\Audit;

/**
 * A tier the audit journal can be read from. The hot local day files
 * ({@see AuditLogStore}) and the MinIO archive ({@see AuditArchiveStore}) expose
 * the same two operations, so {@see AuditQueryService} walks an ordered list of
 * readers instead of branching on which tier owns a given date — a further tier
 * (cold export, second bucket) plugs in without touching the read path.
 */
interface AuditDayReader
{
    /**
     * Decoded rows for one UTC day, in write order.
     *
     * @return iterable<array<string, mixed>>
     */
    public function readDay(string $date): iterable;

    /**
     * Dates this tier holds (UTC Y-m-d), newest first.
     *
     * @return list<string>
     */
    public function days(): array;
}
