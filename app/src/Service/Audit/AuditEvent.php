<?php
declare(strict_types=1);

namespace App\Service\Audit;

/**
 * One recorded action. Immutable value object persisted as a single NDJSON line
 * by {@see AuditLogStore}; the query service reconstructs it from that array.
 *
 * {@see toArray()}/{@see fromArray()} are the on-disk contract — keep them
 * symmetrical (mirrors the analytics {@see \App\Service\Analytics\RequestEvent}).
 * Timestamps are always UTC ISO-8601. The actor is polymorphic: a site account
 * (`user`), the env-defined operator (`admin`), or `anonymous` for pre-auth
 * actions (registration, password reset) which instead carry the target email.
 */
final readonly class AuditEvent
{
    public const ACTOR_USER = 'user';
    public const ACTOR_ADMIN = 'admin';
    public const ACTOR_ANONYMOUS = 'anonymous';

    /** @param array<string, scalar|null> $metadata */
    public function __construct(
        public \DateTimeImmutable $at,
        public string $actorType,
        public ?string $actorId,
        public string $actorLabel,
        public AuditAction $action,
        public AuditOutcome $outcome,
        public ?string $targetType,
        public ?string $targetId,
        public ?string $targetLabel,
        public ?string $ip,
        public ?string $route,
        public array $metadata,
    ) {}

    /** @return array<string, mixed> */
    public function toArray(): array
    {
        return [
            'at' => $this->at->format(\DateTimeInterface::ATOM),
            'actorType' => $this->actorType,
            'actorId' => $this->actorId,
            'actor' => $this->actorLabel,
            'action' => $this->action->value,
            'outcome' => $this->outcome->value,
            'targetType' => $this->targetType,
            'targetId' => $this->targetId,
            'target' => $this->targetLabel,
            'ip' => $this->ip,
            'route' => $this->route,
            'meta' => $this->metadata === [] ? null : $this->metadata,
        ];
    }

    /** @param array<string, mixed> $row */
    public static function fromArray(array $row): self
    {
        return new self(
            at: new \DateTimeImmutable((string) ($row['at'] ?? 'now'), new \DateTimeZone('UTC')),
            actorType: (string) ($row['actorType'] ?? self::ACTOR_ANONYMOUS),
            actorId: self::nullableString($row['actorId'] ?? null),
            actorLabel: (string) ($row['actor'] ?? 'anonyme'),
            action: AuditAction::from((string) $row['action']),
            outcome: AuditOutcome::tryFrom((string) ($row['outcome'] ?? '')) ?? AuditOutcome::Success,
            targetType: self::nullableString($row['targetType'] ?? null),
            targetId: self::nullableString($row['targetId'] ?? null),
            targetLabel: self::nullableString($row['target'] ?? null),
            ip: self::nullableString($row['ip'] ?? null),
            route: self::nullableString($row['route'] ?? null),
            metadata: is_array($row['meta'] ?? null) ? $row['meta'] : [],
        );
    }

    private static function nullableString(mixed $value): ?string
    {
        return $value === null || $value === '' ? null : (string) $value;
    }
}
