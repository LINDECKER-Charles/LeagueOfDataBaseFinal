<?php
declare(strict_types=1);

namespace App\Tests\Unit\Repository;

use App\Entity\ApiKey;
use App\Entity\ApiPlan;
use App\Entity\ApiUsage;
use App\Entity\User;
use App\Repository\ApiKeyRepository;
use App\Tests\Unit\Support\InMemoryOrm;
use Doctrine\ORM\EntityManager;
use Doctrine\Persistence\ManagerRegistry;
use PHPUnit\Framework\TestCase;

/**
 * Fleet-level API reporting against real SQL: active counts, credits in
 * circulation, plan distribution and the api_usage aggregates powering the
 * admin page (all-keys sums, batched per-key usage, top consumers).
 */
final class ApiKeyRepositoryAdminTest extends TestCase
{
    private EntityManager $entityManager;
    private ApiKeyRepository $apiKeys;

    protected function setUp(): void
    {
        $this->entityManager = InMemoryOrm::entityManager([User::class, ApiKey::class, ApiUsage::class]);
        $registry = $this->createStub(ManagerRegistry::class);
        $registry->method('getManagerForClass')->willReturn($this->entityManager);
        $this->apiKeys = new ApiKeyRepository($registry);
    }

    public function testFleetCountersAndPlanDistribution(): void
    {
        $free = $this->givenKey('alice', 'lodb_aaa');
        $paid = $this->givenKey('bob', 'lodb_bbb');
        $paid->applyPlan(ApiPlan::Monthly);
        $revoked = $this->givenKey('carol', 'lodb_ccc');
        $revoked->revoke();
        $this->setCredits($free, 5000);
        $this->setCredits($revoked, 999); // revoked credits are out of circulation
        $this->entityManager->flush();

        self::assertSame(2, $this->apiKeys->countActive());
        self::assertSame(5000, $this->apiKeys->sumActiveCredits());
        self::assertSame(['free' => 1, 'monthly' => 1], $this->apiKeys->countActiveByPlan());
        self::assertSame(3, $this->apiKeys->page(1, 25)['total']);
    }

    public function testUsageAggregatesAcrossKeys(): void
    {
        $a = $this->givenKey('alice', 'lodb_aaa');
        $b = $this->givenKey('bob', 'lodb_bbb');
        $this->givenUsage($a, daysAgo: 0, requests: 100);
        $this->givenUsage($a, daysAgo: 2, requests: 50);
        $this->givenUsage($b, daysAgo: 1, requests: 700);
        $this->givenUsage($b, daysAgo: 45, requests: 9999); // outside the window

        $from = new \DateTimeImmutable('-30 days midnight');

        self::assertSame(850, $this->apiKeys->sumRequestsSince($from));
        self::assertSame(
            [(int) $a->getId() => 150, (int) $b->getId() => 700],
            $this->apiKeys->usageByKeySince([(int) $a->getId(), (int) $b->getId()], $from),
        );
        self::assertSame([], $this->apiKeys->usageByKeySince([], $from));
    }

    public function testTopConsumersRanksByRequests(): void
    {
        $a = $this->givenKey('alice', 'lodb_aaa');
        $b = $this->givenKey('bob', 'lodb_bbb');
        $this->givenUsage($a, daysAgo: 1, requests: 10);
        $this->givenUsage($b, daysAgo: 1, requests: 500);

        $top = $this->apiKeys->topConsumersSince(new \DateTimeImmutable('-30 days midnight'), 5);

        self::assertSame(
            [['keyPrefix' => 'lodb_bbb', 'username' => 'bob', 'requests' => 500],
             ['keyPrefix' => 'lodb_aaa', 'username' => 'alice', 'requests' => 10]],
            $top,
        );
    }

    private function givenKey(string $username, string $prefix): ApiKey
    {
        $user = new User()->setEmail($username . '@example.test')->setUsername($username);
        $this->entityManager->persist($user);
        $key = new ApiKey($user, 'default', hash('sha256', $prefix . bin2hex(random_bytes(8))), $prefix);
        $this->entityManager->persist($key);
        $this->entityManager->flush();

        return $key;
    }

    /** No setter on purpose (credits move via SQL in prod) — reflection for fixtures. */
    private function setCredits(ApiKey $key, int $credits): void
    {
        new \ReflectionProperty(ApiKey::class, 'creditsBalance')->setValue($key, $credits);
    }

    /** api_usage rows are go-api-written in prod (no PHP mutators) — raw insert. */
    private function givenUsage(ApiKey $key, int $daysAgo, int $requests): void
    {
        $this->entityManager->getConnection()->executeStatement(
            'INSERT INTO api_usage (day, requests, api_key_id) VALUES (?, ?, ?)',
            [new \DateTimeImmutable("-{$daysAgo} days")->format('Y-m-d'), $requests, $key->getId()],
        );
    }
}
