<?php
declare(strict_types=1);

namespace App\EventListener;

use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditOutcome;
use Symfony\Component\EventDispatcher\Attribute\AsEventListener;
use Symfony\Component\Security\Http\Authenticator\Passport\Badge\UserBadge;
use Symfony\Component\Security\Http\Event\LoginFailureEvent;
use Symfony\Component\Security\Http\Event\LoginSuccessEvent;
use Symfony\Component\Security\Http\Event\LogoutEvent;

/**
 * Turns the firewall's own authentication events into audit entries. The three
 * highest-value security events (login, failed login, logout) come for free
 * here — no controller touches — and cover both the main and admin firewalls.
 * Failed logins carry the attempted identifier as metadata (actor is anonymous:
 * authentication did not succeed), and login throttling bounds their volume.
 */
final class AuditSecurityListener
{
    public function __construct(private readonly AuditLogger $audit) {}

    #[AsEventListener(event: LoginSuccessEvent::class)]
    public function onLoginSuccess(LoginSuccessEvent $event): void
    {
        $this->audit->logAuth(AuditAction::UserLogin, $event->getUser());
    }

    #[AsEventListener(event: LoginFailureEvent::class)]
    public function onLoginFailure(LoginFailureEvent $event): void
    {
        $this->audit->log(
            AuditAction::UserLoginFailed,
            AuditOutcome::Failure,
            metadata: ['identifier' => $this->attemptedIdentifier($event)],
        );
    }

    #[AsEventListener(event: LogoutEvent::class)]
    public function onLogout(LogoutEvent $event): void
    {
        $user = $event->getToken()?->getUser();
        if ($user !== null) {
            $this->audit->logAuth(AuditAction::UserLogout, $user);
        }
    }

    private function attemptedIdentifier(LoginFailureEvent $event): ?string
    {
        $passport = $event->getPassport();
        if ($passport !== null && $passport->hasBadge(UserBadge::class)) {
            return $passport->getBadge(UserBadge::class)->getUserIdentifier();
        }

        $submitted = $event->getRequest()->request->get('_username');

        return is_string($submitted) && $submitted !== '' ? $submitted : null;
    }
}
