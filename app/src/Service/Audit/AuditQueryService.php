<?php
declare(strict_types=1);

namespace App\Service\Audit;

use App\Entity\User;
use App\Service\Audit\Model\AuditEvent;
use App\Service\Audit\Model\AuditFilter;
use App\Service\Audit\Model\AuditTarget;
use App\Service\Audit\Model\PageWindow;

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
    public function recent(AuditFilter $filter, PageWindow $window): array
    {
        return $this->scan($filter, null, $window);
    }

    /**
     * Actions performed by, or targeting, one account.
     *
     * @return array{rows: list<AuditEvent>, hasMore: bool}
     */
    public function forUser(User $user, AuditFilter $filter, PageWindow $window): array
    {
        return $this->scan($filter, $this->userPredicate($user), $window);
    }

    /**
     * Storage footprint for the purge UI.
     *
     * @return array{
     *     local: array{days: int, bytes: int},
     *     archived: array{days: int, bytes: int},
     *     totalBytes: int,
     *     oldest: ?string,
     *     newest: ?string
     * }
     */
    public function volume(): array
    {
        $localDays = $this->local->days();
        $archivedDays = $this->archive->days();
        $localBytes = array_sum(array_map($this->local->sizeOf(...), $localDays));
        $archivedBytes = array_sum(array_map($this->archive->sizeOf(...), $archivedDays));
        $dates = array_keys($this->index($localDays, $archivedDays)); // newest first

        return [
            'local' => ['days' => count($localDays), 'bytes' => $localBytes],
            'archived' => ['days' => count($archivedDays), 'bytes' => $archivedBytes],
            'totalBytes' => $localBytes + $archivedBytes,
            'oldest' => $dates === [] ? null : $dates[array_key_last($dates)],
            'newest' => $dates[0] ?? null,
        ];
    }

    /**
     * @param null|callable(AuditEvent): bool $predicate
     * @return array{rows: list<AuditEvent>, hasMore: bool}
     */
    private function scan(AuditFilter $filter, ?callable $predicate, PageWindow $window): array
    {
        $limit = $window->scanLimit();
        $matched = [];
        $scanned = 0;

        foreach ($this->index($this->local->days(), $this->archive->days()) as $date => $reader) {
            if ($scanned++ >= self::MAX_DAYS_SCAN || count($matched) >= $limit) {
                break;
            }
            foreach ($this->eventsFrom($reader, $date) as $event) {
                if ($filter->matches($event) && ($predicate === null || $predicate($event))) {
                    $matched[] = $event;
                }
            }
        }

        return $window->cut($matched);
    }

    /** @return list<AuditEvent> newest-first within the day */
    private function eventsFrom(AuditDayReader $reader, string $date): array
    {
        $events = [];
        foreach ($reader->readDay($date) as $row) {
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

    /**
     * Date => the tier that owns it, newest first. Local wins on a date present
     * in both: it is the hot copy of a day archived but not yet pruned. Taking
     * the day lists as arguments keeps this the single union of the two tiers
     * while each store is listed exactly once per query.
     *
     * @param list<string> $localDays
     * @param list<string> $archivedDays
     * @return array<string, AuditDayReader>
     */
    private function index(array $localDays, array $archivedDays): array
    {
        $index = [];
        foreach ($localDays as $date) {
            $index[$date] = $this->local;
        }
        foreach ($archivedDays as $date) {
            $index[$date] ??= $this->archive;
        }
        krsort($index, SORT_STRING);

        return $index;
    }
}
