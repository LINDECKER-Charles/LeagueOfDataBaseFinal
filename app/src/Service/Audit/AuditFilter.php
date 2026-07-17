<?php
declare(strict_types=1);

namespace App\Service\Audit;

/**
 * Read-side filter for the admin journal: an optional category and an inclusive
 * date window. Pure — {@see matches()} is the single place the criteria are
 * applied, so the query service and its tests share one definition.
 */
final readonly class AuditFilter
{
    public function __construct(
        public ?AuditCategory $category = null,
        public ?\DateTimeImmutable $from = null,
        public ?\DateTimeImmutable $to = null,
    ) {}

    public static function none(): self
    {
        return new self();
    }

    public function matches(AuditEvent $event): bool
    {
        if ($this->category !== null && $event->action->category() !== $this->category) {
            return false;
        }
        if ($this->from !== null && $event->at < $this->from) {
            return false;
        }

        return $this->to === null || $event->at <= $this->to;
    }
}
