<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Audit;

use App\Service\Audit\AuditAction;
use App\Service\Audit\AuditArchiveStore;
use App\Service\Audit\AuditEvent;
use App\Service\Audit\AuditLogStore;
use App\Service\Audit\AuditOutcome;
use App\Service\Audit\AuditRollupService;
use League\Flysystem\Filesystem;
use League\Flysystem\Local\LocalFilesystemAdapter;
use PHPUnit\Framework\TestCase;

final class AuditRollupServiceTest extends TestCase
{
    private string $dir;
    private AuditLogStore $local;
    private AuditArchiveStore $archive;
    private AuditRollupService $rollup;
    private string $today;
    private string $yesterday;

    protected function setUp(): void
    {
        $this->dir = sys_get_temp_dir() . '/lodb_audit_' . bin2hex(random_bytes(6));
        $this->local = new AuditLogStore($this->dir);
        $this->archive = new AuditArchiveStore(new Filesystem(new LocalFilesystemAdapter($this->dir . '/minio')));
        $this->rollup = new AuditRollupService($this->local, $this->archive);
        $this->today = gmdate('Y-m-d');
        $this->yesterday = gmdate('Y-m-d', time() - 86400);
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

    private function append(string $date): void
    {
        $this->local->append(new AuditEvent(
            at: new \DateTimeImmutable($date . 'T10:00:00', new \DateTimeZone('UTC')),
            actorType: AuditEvent::ACTOR_USER, actorId: '1', actorLabel: 'Alice',
            action: AuditAction::UserLogin, outcome: AuditOutcome::Success,
            targetType: null, targetId: null, targetLabel: null,
            ip: '203.0.113.1', route: 'app_login', metadata: [],
        ));
    }

    public function testClosedDayIsArchivedVerbatimTodayIsNot(): void
    {
        $this->append($this->yesterday);
        $this->append($this->today);

        $result = $this->rollup->rollup();

        self::assertSame([$this->yesterday], $result['archived']);
        self::assertTrue($this->archive->exists($this->yesterday));
        self::assertFalse($this->archive->exists($this->today));
        // Verbatim: the archived row survives as an AuditEvent, not an aggregate.
        $rows = iterator_to_array($this->archive->readDay($this->yesterday));
        self::assertSame('user.login', $rows[0]['action']);
    }

    public function testPruneRemovesLocalButArchiveSurvives(): void
    {
        $this->append($this->yesterday);

        $this->rollup->rollup(prune: true);

        self::assertNull($this->local->readRaw($this->yesterday));
        self::assertTrue($this->archive->exists($this->yesterday));
    }

    public function testEnforceRetentionDropsDaysBeyondSixMonths(): void
    {
        $old = gmdate('Y-m-d', strtotime('-7 months'));
        $this->append($old);
        $this->append($this->yesterday);

        $result = $this->rollup->enforceRetention();

        self::assertContains($old, $result['deleted']);
        self::assertNotContains($this->yesterday, $result['deleted']);
        self::assertNull($this->local->readRaw($old));
        self::assertNotNull($this->local->readRaw($this->yesterday));
    }

    public function testPurgeBeforeDateDeletesOlderTierBoth(): void
    {
        $this->append($this->yesterday);
        $this->rollup->rollup(); // also archive it

        $result = $this->rollup->purge(new \DateTimeImmutable($this->today, new \DateTimeZone('UTC')));

        self::assertContains($this->yesterday, $result['deleted']);
        self::assertNull($this->local->readRaw($this->yesterday));
        self::assertFalse($this->archive->exists($this->yesterday));
    }

    public function testPurgeAllWipesEverything(): void
    {
        $this->append($this->yesterday);
        $this->append($this->today);
        $this->rollup->rollup();

        $this->rollup->purge(null, all: true);

        self::assertSame([], $this->local->days());
        self::assertSame([], $this->archive->dates());
    }

    public function testPurgeWithoutBoundsIsANoop(): void
    {
        $this->append($this->today);

        $result = $this->rollup->purge(null, all: false);

        self::assertSame([], $result['deleted']);
        self::assertNotNull($this->local->readRaw($this->today));
    }
}
