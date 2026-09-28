<?php
declare(strict_types=1);

namespace App\Tests\Unit\Repository;

use App\Entity\Build;
use App\Entity\User;
use App\Repository\UserRepository;
use App\Tests\Unit\Support\InMemoryOrm;
use Doctrine\ORM\EntityManager;
use Doctrine\Persistence\ManagerRegistry;
use PHPUnit\Framework\TestCase;

/**
 * Admin moderation queries against real SQL (in-memory SQLite): insensitive
 * username/email search with build counts, pagination, and the KPI counters.
 */
final class UserRepositoryAdminTest extends TestCase
{
    private EntityManager $entityManager;
    private UserRepository $users;

    protected function setUp(): void
    {
        $this->entityManager = InMemoryOrm::entityManager([User::class, Build::class]);
        $registry = $this->createStub(ManagerRegistry::class);
        $registry->method('getManagerForClass')->willReturn($this->entityManager);
        $this->users = new UserRepository($registry);
    }

    public function testSearchMatchesUsernameAndEmailCaseInsensitively(): void
    {
        $this->givenUser('RivenMain', 'riven@example.test');
        $this->givenUser('yasuo_fan', 'contact@RIVERSIDE.test');
        $this->givenUser('unrelated', 'someone@example.test');

        $byUsername = $this->users->searchPaginated('riven', 1, 25);
        $byEmail = $this->users->searchPaginated('RIVERSIDE', 1, 25);

        self::assertSame(1, $byUsername['total']);
        self::assertSame('RivenMain', $byUsername['rows'][0]['user']->getUsername());
        self::assertSame(1, $byEmail['total']);
        self::assertSame('yasuo_fan', $byEmail['rows'][0]['user']->getUsername());
    }

    public function testSearchPaginatesNewestFirstAndCountsBuilds(): void
    {
        $old = $this->givenUser('older', 'older@example.test', createdDaysAgo: 5);
        $recent = $this->givenUser('recent', 'recent@example.test', createdDaysAgo: 1);
        $this->givenBuildFor($recent, 'Ahri');
        $this->givenBuildFor($recent, 'Ashe');

        $pageOne = $this->users->searchPaginated('', 1, 1);
        $pageTwo = $this->users->searchPaginated('', 2, 1);

        self::assertSame(2, $pageOne['total']);
        self::assertSame('recent', $pageOne['rows'][0]['user']->getUsername());
        self::assertSame(2, $pageOne['rows'][0]['buildCount']);
        self::assertSame('older', $pageTwo['rows'][0]['user']->getUsername());
        self::assertSame(0, $pageTwo['rows'][0]['buildCount']);
        self::assertSame($old->getId(), $pageTwo['rows'][0]['user']->getId());
    }

    public function testCounters(): void
    {
        $this->givenUser('fresh', 'fresh@example.test', createdDaysAgo: 1);
        $veteran = $this->givenUser('veteran', 'veteran@example.test', createdDaysAgo: 30);
        $veteran->ban('reason kept internal');
        $supporter = $this->givenUser('patron', 'patron@example.test', createdDaysAgo: 10);
        $supporter->setIsSupporter(true);
        $this->entityManager->flush();

        self::assertSame(3, $this->users->countAll());
        self::assertSame(1, $this->users->countBanned());
        self::assertSame(1, $this->users->countSupporters());
        self::assertSame(1, $this->users->countNewSince(new \DateTimeImmutable('-7 days')));
    }

    public function testBanStateRoundTrip(): void
    {
        $user = $this->givenUser('target', 'target@example.test');

        $user->ban(str_repeat('x', 300));
        $this->entityManager->flush();

        self::assertTrue($user->isBanned());
        self::assertNotNull($user->getBannedAt());
        self::assertSame(User::BAN_REASON_MAX_LENGTH, mb_strlen((string) $user->getBanReason()));

        $user->unban();
        $this->entityManager->flush();

        self::assertSame(0, $this->users->countBanned());
    }

    private function givenUser(string $username, string $email, int $createdDaysAgo = 0): User
    {
        $user = new User()->setEmail($email)->setUsername($username);
        if ($createdDaysAgo > 0) {
            // createdAt is immutable in prod; the ordering/window tests need
            // distinct timestamps, hence reflection.
            new \ReflectionProperty(User::class, 'createdAt')
                ->setValue($user, new \DateTimeImmutable("-{$createdDaysAgo} days"));
        }
        $this->entityManager->persist($user);
        $this->entityManager->flush();

        return $user;
    }

    private function givenBuildFor(User $owner, string $championId): void
    {
        $build = new Build()
            ->setOwner($owner)
            ->setName($championId . ' build')
            ->setChampionId($championId)
            ->setGameVersion('16.14.1')
            ->setRunes([])
            ->setSteps([]);
        $this->entityManager->persist($build);
        $this->entityManager->flush();
    }
}
