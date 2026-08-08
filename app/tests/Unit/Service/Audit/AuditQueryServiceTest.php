<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Audit;

use App\Entity\User;
use App\Service\Audit\Model\AuditAction;
use App\Service\Audit\AuditArchiveStore;
use App\Service\Audit\Model\AuditCategory;
use App\Service\Audit\Model\AuditEvent;
use App\Service\Audit\Model\AuditFilter;
use App\Service\Audit\AuditLogStore;
use App\Service\Audit\Model\AuditOutcome;
use App\Service\Audit\AuditQueryService;
use App\Service\Audit\Model\AuditTarget;
use App\Service\Audit\Model\PageWindow;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\TestCase;

final class AuditQueryServiceTest extends TestCase
{
    /** The single day every targeted fixture is stamped on. */
    private const TARGETED_DAY = '2026-07-14';

    private string $dir;
    private AuditLogStore $local;
    private AuditArchiveStore $archive;
    private AuditQueryService $query;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir() . '/lodb_auditq_' . bin2hex(random_bytes(6));
        $this->local = new AuditLogStore($this->dir);
        $this->archive = new AuditArchiveStore(
            new Filesystem(new LocalFilesystemAdapter($this->dir . '/minio')),
        );
        $this->query = new AuditQueryService($this->local, $this->archive);
    }

    protected function tearDown(): void
    {
        if (!is_dir($this->dir)) {
            return;
        }
        $it = new \RecursiveIteratorIterator(
            new \RecursiveDirectoryIterator($this->dir, \FilesystemIterator::SKIP_DOTS),
            \RecursiveIteratorIterator::CHILD_FIRST,
        );
        foreach ($it as $f) {
            $f->isDir() ? rmdir($f->getPathname()) : unlink($f->getPathname());
        }
        rmdir($this->dir);
    }

    /** Records a successful, untargeted action at 10:00 UTC on $date. */
    private function append(string $date, AuditAction $action, ?string $actorId): void
    {
        $this->local->append(new AuditEvent(...self::eventArgs($date, $action, $actorId)));
    }

    /** Same as {@see append()} for the actions that carry a target. */
    private function appendTargeting(
        AuditAction $action,
        ?string $actorId,
        AuditTarget $target,
    ): void {
        $this->local->append(new AuditEvent(...[
            ...self::eventArgs(self::TARGETED_DAY, $action, $actorId),
            'targetType' => $target->type,
            'targetId' => $target->id,
            'targetLabel' => $target->label,
        ]));
    }

    /**
     * Constructor arguments of an untargeted success event, spread as named
     * arguments so the targeted variant only overrides what differs.
     *
     * @return array<string, mixed>
     */
    private static function eventArgs(string $date, AuditAction $action, ?string $actorId): array
    {
        $isAnonymous = $actorId === null;

        return [
            'at' => new \DateTimeImmutable($date . 'T10:00:00', new \DateTimeZone('UTC')),
            'actorType' => $isAnonymous ? AuditEvent::ACTOR_ANONYMOUS : AuditEvent::ACTOR_USER,
            'actorId' => $actorId,
            'actorLabel' => $actorId ?? 'anonyme',
            'action' => $action,
            'outcome' => AuditOutcome::Success,
            'targetType' => null,
            'targetId' => null,
            'targetLabel' => null,
            'ip' => null,
            'route' => null,
            'metadata' => [],
        ];
    }

    private function user(int $id, string $email, string $username): User
    {
        $user = (new User())->setEmail($email)->setUsername($username);
        $ref = new \ReflectionProperty(User::class, 'id');
        $ref->setValue($user, $id);

        return $user;
    }

    public function testRecentReturnsNewestFirstAcrossDays(): void
    {
        $this->append('2026-07-14', AuditAction::UserLogin, '1');
        $this->append('2026-07-16', AuditAction::BuildCreate, '1');

        $rows = $this->query->recent(AuditFilter::none(), new PageWindow(1, 40))['rows'];

        self::assertSame(AuditAction::BuildCreate, $rows[0]->action);
        self::assertSame(AuditAction::UserLogin, $rows[1]->action);
    }

    public function testForUserMatchesActorTargetEmailAndUsername(): void
    {
        $subject = $this->user(7, 'Bob@Example.com', 'BobTheBuilder');
        // performed by the subject
        $this->append('2026-07-14', AuditAction::BuildCreate, '7');
        // pre-auth, targeting the subject's email (case-insensitive)
        $this->appendTargeting(
            AuditAction::UserPasswordReset,
            null,
            AuditTarget::of('user', 99, 'bob@example.com'),
        );
        // an admin action targeting the subject by id
        $this->appendTargeting(
            AuditAction::AdminUserBan,
            'admin',
            AuditTarget::of('user', 7, 'BobTheBuilder'),
        );
        // noise from another account
        $this->append('2026-07-14', AuditAction::BuildCreate, '8');

        $rows = $this->query->forUser($subject, AuditFilter::none(), new PageWindow(1, 40))['rows'];

        self::assertCount(3, $rows);
        foreach ($rows as $row) {
            self::assertNotSame('8', $row->actorId);
        }
    }

    public function testCategoryFilter(): void
    {
        $this->append('2026-07-14', AuditAction::UserLogin, '1');
        $this->append('2026-07-14', AuditAction::BuildCreate, '1');

        $filter = new AuditFilter(AuditCategory::Build);
        $rows = $this->query->recent($filter, new PageWindow(1, 40))['rows'];

        self::assertCount(1, $rows);
        self::assertSame(AuditAction::BuildCreate, $rows[0]->action);
    }

    public function testPaginationCursor(): void
    {
        foreach (range(1, 5) as $i) {
            $this->append('2026-07-14', AuditAction::UserLogin, (string) $i);
        }

        $first = $this->query->recent(AuditFilter::none(), new PageWindow(1, 2));
        self::assertCount(2, $first['rows']);
        self::assertTrue($first['hasMore']);

        $last = $this->query->recent(AuditFilter::none(), new PageWindow(3, 2));
        self::assertCount(1, $last['rows']);
        self::assertFalse($last['hasMore']);
    }

    /**
     * A day archived but not yet pruned exists in both tiers. It must be read
     * once, from the local (hot) copy — never merged with, nor shadowed by, its
     * archived twin.
     */
    public function testDayPresentInBothTiersIsReadFromTheLocalTierOnly(): void
    {
        $this->append('2026-07-14', AuditAction::BuildCreate, '1');
        $this->archive->write('2026-07-14', $this->archivedLine(AuditAction::UserLogout));
        $this->archive->write('2026-07-13', $this->archivedLine(AuditAction::UserLogin));

        $rows = $this->query->recent(AuditFilter::none(), new PageWindow(1, 40))['rows'];

        self::assertSame(
            [AuditAction::BuildCreate, AuditAction::UserLogin],
            array_map(static fn (AuditEvent $e): AuditAction => $e->action, $rows),
        );
    }

    private function archivedLine(AuditAction $action): string
    {
        $event = new AuditEvent(
            at: new \DateTimeImmutable('2026-07-13T08:00:00', new \DateTimeZone('UTC')),
            actorType: AuditEvent::ACTOR_USER,
            actorId: '1', actorLabel: '1',
            action: $action, outcome: AuditOutcome::Success,
            targetType: null, targetId: null, targetLabel: null,
            ip: null, route: null, metadata: [],
        );

        return json_encode($event->toArray(), JSON_THROW_ON_ERROR) . "\n";
    }

    public function testVolumeReportsDaysAndBounds(): void
    {
        $this->append('2026-07-10', AuditAction::UserLogin, '1');
        $this->append('2026-07-12', AuditAction::UserLogin, '1');

        $volume = $this->query->volume();

        self::assertSame(2, $volume['local']['days']);
        self::assertGreaterThan(0, $volume['local']['bytes']);
        self::assertSame('2026-07-10', $volume['oldest']);
        self::assertSame('2026-07-12', $volume['newest']);
    }
}
