<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Audit;

use App\Entity\User;
use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditArchiveStore;
use App\Service\Audit\AuditCategory;
use App\Service\Audit\AuditEvent;
use App\Service\Audit\AuditFilter;
use App\Service\Audit\AuditLogStore;
use App\Service\Audit\AuditOutcome;
use App\Service\Audit\AuditQueryService;
use App\Service\Audit\AuditTarget;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\TestCase;

final class AuditQueryServiceTest extends TestCase
{
    private string $dir;
    private AuditLogStore $local;
    private AuditQueryService $query;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir() . '/lodb_auditq_' . bin2hex(random_bytes(6));
        $this->local = new AuditLogStore($this->dir);
        $archive = new AuditArchiveStore(new Filesystem(new LocalFilesystemAdapter($this->dir . '/minio')));
        $this->query = new AuditQueryService($this->local, $archive);
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

    private function append(string $date, AuditAction $action, ?string $actorId, ?AuditTarget $target = null): void
    {
        $this->local->append(new AuditEvent(
            at: new \DateTimeImmutable($date . 'T10:00:00', new \DateTimeZone('UTC')),
            actorType: $actorId === null ? AuditEvent::ACTOR_ANONYMOUS : AuditEvent::ACTOR_USER,
            actorId: $actorId, actorLabel: $actorId ?? 'anonyme',
            action: $action, outcome: AuditOutcome::Success,
            targetType: $target?->type, targetId: $target?->id, targetLabel: $target?->label,
            ip: null, route: null, metadata: [],
        ));
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

        $rows = $this->query->recent(AuditFilter::none(), 1, 40)['rows'];

        self::assertSame(AuditAction::BuildCreate, $rows[0]->action);
        self::assertSame(AuditAction::UserLogin, $rows[1]->action);
    }

    public function testForUserMatchesActorTargetEmailAndUsername(): void
    {
        $subject = $this->user(7, 'Bob@Example.com', 'BobTheBuilder');
        // performed by the subject
        $this->append('2026-07-14', AuditAction::BuildCreate, '7');
        // pre-auth, targeting the subject's email (case-insensitive)
        $this->append('2026-07-14', AuditAction::UserPasswordReset, null, AuditTarget::of('user', 99, 'bob@example.com'));
        // an admin action targeting the subject by id
        $this->append('2026-07-14', AuditAction::AdminUserBan, 'admin', AuditTarget::of('user', 7, 'BobTheBuilder'));
        // noise from another account
        $this->append('2026-07-14', AuditAction::BuildCreate, '8');

        $rows = $this->query->forUser($subject, AuditFilter::none(), 1, 40)['rows'];

        self::assertCount(3, $rows);
        foreach ($rows as $row) {
            self::assertNotSame('8', $row->actorId);
        }
    }

    public function testCategoryFilter(): void
    {
        $this->append('2026-07-14', AuditAction::UserLogin, '1');
        $this->append('2026-07-14', AuditAction::BuildCreate, '1');

        $rows = $this->query->recent(new AuditFilter(AuditCategory::Build), 1, 40)['rows'];

        self::assertCount(1, $rows);
        self::assertSame(AuditAction::BuildCreate, $rows[0]->action);
    }

    public function testPaginationCursor(): void
    {
        foreach (range(1, 5) as $i) {
            $this->append('2026-07-14', AuditAction::UserLogin, (string) $i);
        }

        $first = $this->query->recent(AuditFilter::none(), 1, 2);
        self::assertCount(2, $first['rows']);
        self::assertTrue($first['hasMore']);

        $last = $this->query->recent(AuditFilter::none(), 3, 2);
        self::assertCount(1, $last['rows']);
        self::assertFalse($last['hasMore']);
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
