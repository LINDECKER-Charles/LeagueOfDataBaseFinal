<?php
declare(strict_types=1);

namespace App\Service\Audit\Model;


/**
 * The identity an audited action is attributed to, as resolved from the
 * security token by {@see \App\Service\Audit\AuditActorResolver}.
 *
 * A value object rather than an array: its three fields are copied straight
 * into every {@see AuditEvent}, so a mistyped key must not be able to reach
 * the journal. The named constructors are the only places that decide which
 * `AuditEvent::ACTOR_*` shape applies.
 */
final readonly class AuditActor
{
    public function __construct(
        public string $type,
        public ?string $id,
        public string $label,
    ) {}

    public static function user(string $id, string $label): self
    {
        return new self(AuditEvent::ACTOR_USER, $id, $label);
    }

    /** The env-defined operator: its identifier is both id and display label. */
    public static function admin(string $identifier): self
    {
        return new self(AuditEvent::ACTOR_ADMIN, $identifier, $identifier);
    }

    /** Pre-auth actions (registration, password reset) have no acting account. */
    public static function anonymous(): self
    {
        return new self(AuditEvent::ACTOR_ANONYMOUS, null, 'anonyme');
    }
}
