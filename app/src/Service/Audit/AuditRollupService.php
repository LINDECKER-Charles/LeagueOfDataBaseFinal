<?php
declare(strict_types=1);

namespace App\Service\Audit;

/**
 * Durability + retention for the audit journal. Archives closed local NDJSON
 * days into MinIO verbatim, and enforces the legal retention ceiling.
 *
 * Retention is fixed to the CNIL recommendation for security / connection logs:
 * six months. (Some security contexts justify twelve; this is the conservative
 * default and the single source of truth — change {@see RETENTION_PERIOD} only.)
 * Days past the ceiling are deleted from both tiers by {@see enforceRetention()},
 * run on schedule; {@see purge()} is the operator's on-demand reclaim.
 */
final class AuditRollupService
{
    /** ISO-8601 duration. CNIL baseline for security logs. */
    public const RETENTION_PERIOD = 'P6M';

    public function __construct(
        private readonly AuditLogStore $local,
        private readonly AuditArchiveStore $archive,
    ) {}

    /**
     * Archive every closed local day (never today, still open) into MinIO.
     * Idempotent: an already-archived day is skipped unless it is still local
     * and unpruned. With $prune, the local copy is removed once safely archived.
     *
     * @return array{archived: list<string>, pruned: list<string>}
     */
    public function rollup(bool $prune = false): array
    {
        $today = gmdate('Y-m-d');
        $archived = $pruned = [];

        foreach ($this->local->days() as $date) {
            if ($date === $today) {
                continue;
            }

            if (!$this->archive->exists($date)) {
                $raw = $this->local->readRaw($date);
                if ($raw === null) {
                    continue;
                }
                $this->archive->write($date, $raw);
                $archived[] = $date;
            }

            if ($prune) {
                $this->local->deleteDay($date);
                $pruned[] = $date;
            }
        }

        return ['archived' => $archived, 'pruned' => $pruned];
    }

    /**
     * Delete every day older than the CNIL ceiling from both tiers.
     *
     * @return array{deleted: list<string>, freedBytes: int}
     */
    public function enforceRetention(): array
    {
        $cutoff = $this->retentionCutoff()->format('Y-m-d');

        return $this->purgeMatching(static fn (string $date): bool => $date < $cutoff);
    }

    /**
     * Operator-triggered purge. $all wipes both tiers entirely; otherwise every
     * day strictly before $before is removed. Returns a receipt for the flash.
     *
     * @return array{deleted: list<string>, freedBytes: int}
     */
    public function purge(?\DateTimeImmutable $before, bool $all = false): array
    {
        if (!$all && $before === null) {
            return ['deleted' => [], 'freedBytes' => 0];
        }

        $beforeDate = $before?->format('Y-m-d') ?? '';

        return $this->purgeMatching(static fn (string $date): bool => $all || $date < $beforeDate);
    }

    public function retentionCutoff(): \DateTimeImmutable
    {
        $now = new \DateTimeImmutable('now', new \DateTimeZone('UTC'));

        return $now->sub(new \DateInterval(self::RETENTION_PERIOD));
    }

    /**
     * @param callable(string): bool $shouldDelete
     * @return array{deleted: list<string>, freedBytes: int}
     */
    private function purgeMatching(callable $shouldDelete): array
    {
        $deleted = [];
        $freedBytes = 0;

        foreach ($this->allDates() as $date) {
            if (!$shouldDelete($date)) {
                continue;
            }
            $freedBytes += $this->local->sizeOf($date) + $this->archive->sizeOf($date);
            $this->local->deleteDay($date);
            $this->archive->deleteDay($date);
            $deleted[] = $date;
        }
        sort($deleted);

        return ['deleted' => $deleted, 'freedBytes' => $freedBytes];
    }

    /** Union of local and archived dates, deduplicated. @return list<string> */
    private function allDates(): array
    {
        return array_values(array_unique([...$this->local->days(), ...$this->archive->dates()]));
    }
}
