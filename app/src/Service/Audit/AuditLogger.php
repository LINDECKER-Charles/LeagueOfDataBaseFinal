<?php
declare(strict_types=1);

namespace App\Service\Audit;

use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\Security\Core\User\UserInterface;

/**
 * The single write path of the audit journal. Call sites declare intent in one
 * line; all the plumbing (actor resolution, IP/route capture, timestamping,
 * NDJSON append) lives here so the knowledge of "how an action is recorded"
 * exists in exactly one place.
 *
 * Every write is best-effort: an audit failure must never turn the action it
 * describes into an error, so exceptions are swallowed.
 */
final class AuditLogger
{
    public function __construct(
        private readonly AuditLogStore $store,
        private readonly AuditActorResolver $actors,
        private readonly RequestStack $requestStack,
    ) {}

    /**
     * Record an action attributed to the current security token (the typical
     * controller call). Pre-auth actions resolve to an anonymous actor — pass the
     * subject through $target so they remain searchable.
     *
     * @param array<string, scalar|null> $metadata
     */
    public function log(
        AuditAction $action,
        AuditOutcome $outcome = AuditOutcome::Success,
        ?AuditTarget $target = null,
        array $metadata = [],
    ): void {
        $this->record($this->actors->resolve(), $action, $outcome, $target, $metadata);
    }

    /**
     * Record an authentication action with an explicit actor — used by the
     * security listener, where the token storage is not yet reliably populated
     * but the authenticated user is carried by the event.
     */
    public function logAuth(AuditAction $action, UserInterface $actor, AuditOutcome $outcome = AuditOutcome::Success): void
    {
        $this->record($this->actors->resolve($actor), $action, $outcome, null, []);
    }

    /**
     * @param array{type: string, id: ?string, label: string} $actor
     * @param array<string, scalar|null> $metadata
     */
    private function record(array $actor, AuditAction $action, AuditOutcome $outcome, ?AuditTarget $target, array $metadata): void
    {
        try {
            $request = $this->requestStack->getMainRequest();
            $this->store->append(new AuditEvent(
                at: new \DateTimeImmutable('now', new \DateTimeZone('UTC')),
                actorType: $actor['type'],
                actorId: $actor['id'],
                actorLabel: $actor['label'],
                action: $action,
                outcome: $outcome,
                targetType: $target?->type,
                targetId: $target?->id,
                targetLabel: $target?->label,
                ip: $request?->getClientIp(),
                route: $request !== null ? self::routeOf($request) : null,
                metadata: $metadata,
            ));
        } catch (\Throwable) {
            // Audit is best-effort; never propagate into the request lifecycle.
        }
    }

    private static function routeOf(Request $request): ?string
    {
        $route = $request->attributes->get('_route');

        return is_string($route) && $route !== '' ? $route : null;
    }
}
