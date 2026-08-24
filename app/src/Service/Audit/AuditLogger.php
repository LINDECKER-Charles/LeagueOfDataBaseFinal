<?php
declare(strict_types=1);

namespace App\Service\Audit;

use App\Service\Audit\Model\AuditAction;
use App\Service\Audit\Model\AuditEvent;
use App\Service\Audit\Model\AuditOutcome;
use App\Service\Audit\Model\AuditTarget;
use Monolog\Attribute\WithMonologChannel;
use Psr\Log\LoggerInterface;
use Psr\Log\LogLevel;
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
 * describes into an error, so exceptions are swallowed — and, since the trail is
 * legally retained, the loss is reported on the `audit` channel instead of
 * vanishing.
 */
#[WithMonologChannel('audit')]
final class AuditLogger
{
    public function __construct(
        private readonly AuditLogStore $store,
        private readonly AuditActorResolver $actors,
        private readonly RequestStack $requestStack,
        private readonly LoggerInterface $logger,
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
        $this->record(null, $action, $outcome, $target, $metadata);
    }

    /**
     * Record an authentication action with an explicit actor — used by the
     * security listener, where the token storage is not yet reliably populated
     * but the authenticated user is carried by the event.
     */
    public function logAuth(
        AuditAction $action,
        UserInterface $actor,
        AuditOutcome $outcome = AuditOutcome::Success,
    ): void {
        $this->record($actor, $action, $outcome, null, []);
    }

    /**
     * The actor arrives UNRESOLVED on purpose: resolution reads the security
     * token and can itself raise while an authentication failure is being
     * handled, so it has to happen inside the try — the previous shape left it
     * outside, which made the best-effort contract a half-truth.
     *
     * @param array<string, scalar|null> $metadata
     */
    private function record(
        ?UserInterface $actor,
        AuditAction $action,
        AuditOutcome $outcome,
        ?AuditTarget $target,
        array $metadata,
    ): void {
        try {
            $request = $this->requestStack->getMainRequest();
            $resolved = $this->actors->resolve($actor);
            $event = new AuditEvent(
                at: new \DateTimeImmutable('now', new \DateTimeZone('UTC')),
                actorType: $resolved->type,
                actorId: $resolved->id,
                actorLabel: $resolved->label,
                action: $action,
                outcome: $outcome,
                targetType: $target?->type,
                targetId: $target?->id,
                targetLabel: $target?->label,
                ip: $request?->getClientIp(),
                route: $request !== null ? self::routeOf($request) : null,
                metadata: $metadata,
            );
            $this->store->append($event);
        } catch (\Throwable $e) {
            // Audit is best-effort; never propagate into the request lifecycle.
            // `critical` all the same: the journal is legally retained and nothing
            // here repairs itself.
            $this->logger->critical('audit.journal.write_failed', [
                'action' => $action->value,
                'exception' => $e,
            ]);

            return;
        }

        $this->mirror($event);
    }

    /**
     * Observability copy of a recorded action, on the `audit` Monolog channel, so
     * security events sit on the same timeline as the infrastructure metrics.
     *
     * DELIBERATELY PARTIAL, and built from an explicit allow-list rather than by
     * subtracting keys from {@see AuditEvent::toArray()}: a field added to the
     * event later must not leak here by default.
     *
     * `ip` is dropped, and so is `meta.identifier` — the address typed on
     * `user.login_failed`, the journal's only direct PII and precisely the event
     * worth mirroring. Labels go too: they carry display names. The NDJSON file
     * remains the legal source (CNIL retention); this copy lives in a 90-day
     * searchable index outside that perimeter.
     *
     * The event key keeps the on-disk `AuditAction` value (`audit.user.login_failed`)
     * — a closed contract, not reworded to look tidy in a log.
     */
    private function mirror(AuditEvent $event): void
    {
        $metadata = $event->metadata;
        unset($metadata['identifier']);

        $this->logger->log(
            // Not a `match`: anything that is not a success is a warning, so a
            // fourth outcome would land on the safe side instead of raising.
            $event->outcome === AuditOutcome::Success ? LogLevel::INFO : LogLevel::WARNING,
            'audit.' . $event->action->value,
            array_filter([
                'actor_type' => $event->actorType,
                'actor_id' => $event->actorId,
                'outcome' => $event->outcome->value,
                'target_type' => $event->targetType,
                'target_id' => $event->targetId,
                'route' => $event->route,
                'meta' => $metadata === [] ? null : $metadata,
            ], static fn (mixed $value): bool => $value !== null),
        );
    }

    private static function routeOf(Request $request): ?string
    {
        $route = $request->attributes->get('_route');

        return is_string($route) && $route !== '' ? $route : null;
    }
}
