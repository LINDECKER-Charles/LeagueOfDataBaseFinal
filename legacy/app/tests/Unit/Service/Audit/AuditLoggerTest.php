<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Audit;

use App\Entity\User;
use App\Service\Audit\AuditActorResolver;
use App\Service\Audit\AuditLogger;
use App\Service\Audit\AuditLogStore;
use App\Service\Audit\Model\AuditAction;
use App\Service\Audit\Model\AuditOutcome;
use App\Service\Audit\Model\AuditTarget;
use App\Tests\Unit\Support\RecordingLogger;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;
use Psr\Log\LogLevel;
use Symfony\Bundle\SecurityBundle\Security;
use Symfony\Component\DependencyInjection\ServiceLocator;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\RequestStack;
use Symfony\Component\Security\Core\Authentication\Token\Storage\TokenStorage;
use Symfony\Component\Security\Core\User\UserInterface;

/**
 * The audit journal mirror: security events must reach supervision on the same
 * timeline as the infrastructure metrics, WITHOUT carrying the journal's personal
 * data along. The NDJSON file stays the legal source (CNIL retention); the mirror
 * lands in a 90-day searchable index outside that perimeter, so what it must
 * never contain is a guarantee — and a code review is not a mechanism that keeps
 * guarantees.
 */
final class AuditLoggerTest extends TestCase
{
    private const ATTEMPTED_EMAIL = 'victim@example.com';
    private const CLIENT_IP = '203.0.113.7';

    /** @var list<string> */
    private array $tempDirs = [];

    protected function tearDown(): void
    {
        foreach ($this->tempDirs as $dir) {
            foreach (glob($dir . '/var/state/audit/events/*.ndjson') ?: [] as $file) {
                @unlink($file);
            }
        }
        $this->tempDirs = [];
    }

    /**
     * THE test of this lot. `meta.identifier` carries the address typed in the
     * login form — the journal's only direct PII — and it rides on exactly the
     * event worth mirroring.
     */
    public function testTheMirrorCarriesNeitherTheClientIpNorTheAttemptedIdentifier(): void
    {
        $logger = new RecordingLogger();

        $this->auditWith($logger)->log(
            AuditAction::UserLoginFailed,
            AuditOutcome::Failure,
            metadata: ['identifier' => self::ATTEMPTED_EMAIL],
        );

        $record = $logger->only('audit.user.login_failed');
        $serialised = json_encode($record, JSON_THROW_ON_ERROR);

        self::assertArrayNotHasKey('ip', $record['context']);
        self::assertArrayNotHasKey('identifier', $record['context']['meta'] ?? []);
        self::assertStringNotContainsString(self::ATTEMPTED_EMAIL, $serialised);
        self::assertStringNotContainsString(self::CLIENT_IP, $serialised);
    }

    /** A metadata key that is not the identifier is kept: it is what makes the line useful. */
    public function testTheMirrorKeepsTheRestOfTheMetadata(): void
    {
        $logger = new RecordingLogger();

        $this->auditWith($logger)->log(
            AuditAction::ProfileUpdate,
            metadata: ['section' => 'password', 'identifier' => self::ATTEMPTED_EMAIL],
        );

        self::assertSame(
            ['section' => 'password'],
            $logger->only('audit.profile.update')['context']['meta'],
        );
    }

    /** Display labels are names; only internal ids may travel. */
    public function testTheMirrorCarriesIdsAndNeverDisplayLabels(): void
    {
        $logger = new RecordingLogger();

        $this->auditWith($logger)->log(
            AuditAction::AdminUserBan,
            target: new AuditTarget(AuditTarget::TYPE_USER, '42', 'SomePlayerName'),
        );

        $context = $logger->only('audit.admin.user_ban')['context'];

        self::assertSame('42', $context['target_id']);
        self::assertSame(AuditTarget::TYPE_USER, $context['target_type']);
        self::assertStringNotContainsString(
            'SomePlayerName',
            json_encode($context, JSON_THROW_ON_ERROR),
        );
    }

    /**
     * The enum has THREE cases, and `Denied` — an authorization or CSRF refusal —
     * is the most interesting security event of the set.
     *
     * @return iterable<string, array{AuditOutcome, string}>
     */
    public static function outcomes(): iterable
    {
        yield 'a success is routine' => [AuditOutcome::Success, LogLevel::INFO];
        yield 'a failure is worth seeing' => [AuditOutcome::Failure, LogLevel::WARNING];
        yield 'a refusal is worth seeing' => [AuditOutcome::Denied, LogLevel::WARNING];
    }

    #[DataProvider('outcomes')]
    public function testTheOutcomeDrivesTheLevel(AuditOutcome $outcome, string $expected): void
    {
        $logger = new RecordingLogger();

        $this->auditWith($logger)->log(AuditAction::BuildDelete, $outcome);

        self::assertSame($expected, $logger->only('audit.build.delete')['level']);
    }

    /** The key is the on-disk AuditAction value, prefixed — a closed contract. */
    public function testTheEventKeyMirrorsTheOnDiskActionValue(): void
    {
        $logger = new RecordingLogger();

        $this->auditWith($logger)->log(AuditAction::ApiKeyRevoke);

        self::assertSame(['audit.apikey.revoke'], $logger->keys());
    }

    /**
     * A journal that cannot be written is the one failure nobody may discover
     * months later — and it still must not break the action it describes.
     */
    public function testAnUnwritableJournalIsReportedAndSwallowed(): void
    {
        $logger = new RecordingLogger();
        // A regular file as project dir: mkdir("<file>/var/state/…") cannot succeed.
        $unwritable = (string) tempnam(sys_get_temp_dir(), 'lodb-audit-');

        try {
            $audit = new AuditLogger(
                new AuditLogStore($unwritable),
                $this->resolver(),
                new RequestStack(),
                $logger,
            );
            $audit->log(AuditAction::UserRegister);
        } finally {
            @unlink($unwritable);
        }

        self::assertSame(
            LogLevel::CRITICAL,
            $logger->only('audit.journal.write_failed')['level'],
        );
        self::assertSame(
            [],
            $logger->recordsFor('audit.user.register'),
            'a journal write that failed must not be mirrored as if it had succeeded',
        );
    }

    /**
     * The best-effort contract used to be a half-truth: actor resolution ran
     * OUTSIDE the try, and it reads the security token — which can raise exactly
     * while an authentication failure is being handled.
     */
    public function testAFailingActorResolutionNeverReachesTheCaller(): void
    {
        $logger = new RecordingLogger();
        $exploding = new class (new ServiceLocator([])) extends Security {
            public function getUser(): ?UserInterface
            {
                throw new \RuntimeException('token storage is not usable here');
            }
        };

        $audit = new AuditLogger(
            $this->store(),
            new AuditActorResolver($exploding),
            new RequestStack(),
            $logger,
        );
        $audit->log(AuditAction::UserLoginFailed, AuditOutcome::Failure);

        self::assertSame(['audit.journal.write_failed'], $logger->keys());
    }

    /** An explicit actor (the security listener's path) is attributed, not anonymous. */
    public function testAnExplicitActorIsAttributedByItsType(): void
    {
        $logger = new RecordingLogger();
        $user = new User();
        $user->setEmail('someone@example.com');
        $user->setUsername('someone');

        $this->auditWith($logger)->logAuth(AuditAction::UserLogin, $user);

        self::assertSame('user', $logger->only('audit.user.login')['context']['actor_type']);
    }

    /** A request carrying a real client IP: the mirror must still drop it. */
    private function auditWith(RecordingLogger $logger): AuditLogger
    {
        $stack = new RequestStack();
        $request = Request::create('/login', 'POST');
        $request->server->set('REMOTE_ADDR', self::CLIENT_IP);
        $stack->push($request);

        return new AuditLogger($this->store(), $this->resolver(), $stack, $logger);
    }

    /** Real store on a throwaway project dir: the write path is exercised for real. */
    private function store(): AuditLogStore
    {
        $dir = sys_get_temp_dir() . '/lodb-audit-' . bin2hex(random_bytes(6));
        $this->tempDirs[] = $dir;

        return new AuditLogStore($dir);
    }

    private function resolver(): AuditActorResolver
    {
        $tokens = new TokenStorage();

        return new AuditActorResolver(new Security(new ServiceLocator([
            'security.token_storage' => static fn (): TokenStorage => $tokens,
        ])));
    }
}
