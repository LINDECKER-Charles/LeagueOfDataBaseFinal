<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Audit;

use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditEvent;
use App\Service\Audit\AuditOutcome;
use PHPUnit\Framework\TestCase;

final class AuditEventTest extends TestCase
{
    private function event(): AuditEvent
    {
        return new AuditEvent(
            at: new \DateTimeImmutable('2026-07-15T10:00:00', new \DateTimeZone('UTC')),
            actorType: AuditEvent::ACTOR_USER,
            actorId: '42',
            actorLabel: 'Alice#EUW',
            action: AuditAction::AdminUserBan,
            outcome: AuditOutcome::Success,
            targetType: 'user',
            targetId: '7',
            targetLabel: 'Bob',
            ip: '203.0.113.9',
            route: 'admin_user_ban',
            metadata: ['reason' => 'spam'],
        );
    }

    public function testToArrayFromArrayRoundTrip(): void
    {
        $original = $this->event();

        $restored = AuditEvent::fromArray($original->toArray());

        self::assertEquals($original->at, $restored->at);
        self::assertSame($original->actorType, $restored->actorType);
        self::assertSame($original->actorId, $restored->actorId);
        self::assertSame($original->actorLabel, $restored->actorLabel);
        self::assertSame(AuditAction::AdminUserBan, $restored->action);
        self::assertSame(AuditOutcome::Success, $restored->outcome);
        self::assertSame('user', $restored->targetType);
        self::assertSame('7', $restored->targetId);
        self::assertSame('Bob', $restored->targetLabel);
        self::assertSame('203.0.113.9', $restored->ip);
        self::assertSame('admin_user_ban', $restored->route);
        self::assertSame(['reason' => 'spam'], $restored->metadata);
    }

    public function testEmptyMetadataIsNormalisedNotStored(): void
    {
        $event = new AuditEvent(
            at: new \DateTimeImmutable('2026-07-15T10:00:00', new \DateTimeZone('UTC')),
            actorType: AuditEvent::ACTOR_ANONYMOUS,
            actorId: null,
            actorLabel: 'anonyme',
            action: AuditAction::UserLoginFailed,
            outcome: AuditOutcome::Failure,
            targetType: null,
            targetId: null,
            targetLabel: null,
            ip: null,
            route: 'app_login',
            metadata: [],
        );

        $array = $event->toArray();
        self::assertNull($array['meta']);
        self::assertSame([], AuditEvent::fromArray($array)->metadata);
        self::assertNull(AuditEvent::fromArray($array)->actorId);
    }

    public function testUnknownOutcomeFallsBackToSuccess(): void
    {
        $restored = AuditEvent::fromArray([
            'at' => '2026-07-15T10:00:00+00:00',
            'action' => 'user.login',
            'outcome' => 'bogus',
        ]);

        self::assertSame(AuditOutcome::Success, $restored->outcome);
        self::assertSame(AuditAction::UserLogin, $restored->action);
    }
}
