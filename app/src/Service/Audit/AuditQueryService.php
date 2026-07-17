<?php
declare(strict_types=1);

namespace App\Service\Audit;

use App\Entity\User;

/**
 * Read side of the audit journal. Merges the local (hot) and MinIO (archived)
 * tiers, newest first, and filters in memory. There is no index — CLAUDE.md
 * mandates file storage, not Postgres, for this data — so reads are a bounded
 * scan: days are walked newest-first and capped. That is acceptable for an
 * admin-only, low-traffic surface whose volume is bounded by the retention
 * ceiling; it is the deliberate trade-off of the no-database constraint.
 */
final class AuditQueryService
{
    /** Hard bound on days walked per query (retention keeps real counts ~180). */
    private const MAX_DAYS_SCAN = 400;

    public function __construct(
        private readonly AuditLogStore $local,
        private readonly AuditArchiveStore $archive,
    ) {}

    /** @return array{rows: list<AuditEvent>, hasMore: bool} */
    public function recent(AuditFilter $filter, int $page, int $perPage): array
    {
        return $this->scan($filter, null, $page, $perPage);
    }

    /** Actions performed by, or targeting, one account. @return array{rows: list<AuditEvent>, hasMore: bool} */
    public function forUser(User $user, AuditFilter $filter, int $page, int $perPage): array
    {
        return $this->scan($filter, $this->userPredicate($user), $page, $perPage);
    }

    /**
     * Storage footprint for the purge UI.
     *
     * @return array{local: array{days: int, bytes: int}, archived: array{days: int, bytes: int}, totalBytes: int, oldest: ?string, newest: ?string}
     */
    public function volume(): array
    {
        $localDays = $this->local->days();
        $archivedDates = $this->archive->dates();
        $localBytes = array_sum(array_map($this->local->sizeOf(...), $localDays));
        $archivedBytes = array_sum(array_map($this->archive->sizeOf(...), $archivedDates));
        $all = array_values(array_unique([...$localDays, ...$archivedDates]));
        sort($all);

        return [
            'local' => ['days' => count($localDays), 'bytes' => $localBytes],
            'archived' => ['days' => count($archivedDates), 'bytes' => $archivedBytes],
            'totalBytes' => $localBytes + $archivedBytes,
            'oldest' => $all[0] ?? null,
            'newest' => $all === [] ? null : $all[array_key_last($all)],
        ];
    }

    /**
     * @param null|callable(AuditEvent): bool $predicate
     * @return array{rows: list<AuditEvent>, hasMore: bool}
     */
    private function scan(AuditFilter $filter, ?callable $predicate, int $page, int $perPage): array
    {
        $page = max(1, $page);
        $offset = ($page - 1) * $perPage;
        $limit = $offset + $perPage + 1; // one extra row proves a next page exists
        $localSet = array_flip($this->local->days());
        $matched = [];

        foreach ($this->dates() as $i => $date) {
            if ($i >= self::MAX_DAYS_SCAN || count($matched) >= $limit) {
                break;
            }
            foreach ($this->eventsForDate($date, isset($localSet[$date])) as $event) {
                if ($filter->matches($event) && ($predicate === null || $predicate($event))) {
                    $matched[] = $event;
                }
            }
        }

        return [
            'rows' => array_slice($matched, $offset, $perPage),
            'hasMore' => count($matched) > $offset + $perPage,
        ];
    }

    /** @return list<AuditEvent> newest-first within the day */
    private function eventsForDate(string $date, bool $inLocal): array
    {
        $rows = $inLocal ? $this->local->readDay($date) : $this->archive->readDay($date);
        $events = [];
        foreach ($rows as $row) {
            try {
                $events[] = AuditEvent::fromArray($row);
            } catch (\Throwable) {
                // Skip malformed / unknown-action lines rather than fail the page.
            }
        }

        return array_reverse($events);
    }

    /** @return callable(AuditEvent): bool */
    private function userPredicate(User $user): callable
    {
        $id = (string) $user->getId();
        $email = mb_strtolower($user->getEmail());
        $username = mb_strtolower($user->getUsername());

        return static function (AuditEvent $event) use ($id, $email, $username): bool {
            if ($event->actorType === AuditEvent::ACTOR_USER && $event->actorId === $id) {
                return true;
            }
            if ($event->targetType === AuditTarget::TYPE_USER && $event->targetId === $id) {
                return true;
            }
            $label = mb_strtolower((string) $event->targetLabel);

            return $label !== '' && ($label === $email || $label === $username);
        };
    }

    /** Union of local + archived dates, newest first. @return list<string> */
    private function dates(): array
    {
        $dates = array_values(array_unique([...$this->local->days(), ...$this->archive->dates()]));
        rsort($dates);

        return $dates;
    }
}
