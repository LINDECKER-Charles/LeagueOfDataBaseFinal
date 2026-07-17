<?php
declare(strict_types=1);

namespace App\Tests\Unit\Repository;

use App\Entity\Donation;
use App\Entity\User;
use App\Repository\DonationRepository;
use App\Tests\Unit\Support\InMemoryOrm;
use Doctrine\ORM\EntityManager;
use Doctrine\Persistence\ManagerRegistry;
use PHPUnit\Framework\TestCase;

/**
 * Admin donation reporting against real SQL: zero-filled daily series,
 * identified-vs-anonymous split and newest-first pagination.
 */
final class DonationRepositoryAdminTest extends TestCase
{
    private EntityManager $entityManager;
    private DonationRepository $donations;

    protected function setUp(): void
    {
        $this->entityManager = InMemoryOrm::entityManager([User::class, Donation::class]);
        $registry = $this->createStub(ManagerRegistry::class);
        $registry->method('getManagerForClass')->willReturn($this->entityManager);
        $this->donations = new DonationRepository($registry);
    }

    public function testDailyTotalsAreContinuousZeroFilledAndSummed(): void
    {
        $this->givenDonation(500, daysAgo: 1);
        $this->givenDonation(300, daysAgo: 1);
        $this->givenDonation(200, daysAgo: 40); // outside the window

        $series = $this->donations->dailyTotals(30);

        self::assertCount(30, $series);
        $yesterday = new \DateTimeImmutable('-1 day')->format('Y-m-d');
        self::assertSame(800, $series[$yesterday]);
        self::assertSame(800, array_sum($series));
        self::assertSame(0, $series[array_key_first($series)]);
    }

    public function testDonorSplitCountsDistinctIdentifiedAndAnonymous(): void
    {
        $donor = $this->givenUser('generous');
        $this->givenDonation(500, user: $donor);
        $this->givenDonation(700, user: $donor);
        $this->givenDonation(200);

        self::assertSame(1, $this->donations->countDistinctDonors());
        self::assertSame(1, $this->donations->countAnonymous());
        self::assertSame(3, $this->donations->countAll());
        self::assertSame(1400, $this->donations->sumAll());
    }

    public function testPageIsNewestFirst(): void
    {
        $this->givenDonation(100, daysAgo: 3);
        $this->givenDonation(200, daysAgo: 1);
        $this->givenDonation(300, daysAgo: 2);

        ['donations' => $rows, 'total' => $total] = $this->donations->page(1, 2);

        self::assertSame(3, $total);
        self::assertSame([200, 300], array_map(static fn (Donation $d): int => $d->getAmountCents(), $rows));
    }

    private function givenUser(string $username): User
    {
        $user = new User()->setEmail($username . '@example.test')->setUsername($username);
        $this->entityManager->persist($user);
        $this->entityManager->flush();

        return $user;
    }

    private function givenDonation(int $cents, int $daysAgo = 0, ?User $user = null): Donation
    {
        $donation = new Donation('cs_' . bin2hex(random_bytes(8)), $cents, 'eur', $user);
        if ($daysAgo > 0) {
            // Donations are immutable in prod (webhook-written); the window
            // tests need past dates, hence reflection.
            new \ReflectionProperty(Donation::class, 'createdAt')
                ->setValue($donation, new \DateTimeImmutable("-{$daysAgo} days")->setTime(12, 0));
        }
        $this->entityManager->persist($donation);
        $this->entityManager->flush();

        return $donation;
    }
}
