<?php
declare(strict_types=1);

namespace App\Repository;

use App\Entity\Donation;
use App\Entity\User;
use Doctrine\Bundle\DoctrineBundle\Repository\ServiceEntityRepository;
use Doctrine\Persistence\ManagerRegistry;

/**
 * @extends ServiceEntityRepository<Donation>
 */
final class DonationRepository extends ServiceEntityRepository
{
    public function __construct(ManagerRegistry $registry)
    {
        parent::__construct($registry, Donation::class);
    }

    /** Idempotency probe for the webhook: has this Checkout session already been recorded? */
    public function existsBySessionId(string $sessionId): bool
    {
        return $this->count(['stripeSessionId' => $sessionId]) > 0;
    }

    /** Lifetime donated amount (cents) linked to the account. */
    public function sumForUser(User $user): int
    {
        return (int) $this->createQueryBuilder('d')
            ->select('COALESCE(SUM(d.amountCents), 0)')
            ->andWhere('d.user = :user')
            ->setParameter('user', $user)
            ->getQuery()
            ->getSingleScalarResult();
    }

    /** Total number of recorded donations (anonymous included) — admin reporting. */
    public function countAll(): int
    {
        return $this->count([]);
    }

    /** Grand total (cents) of every recorded donation — admin reporting. */
    public function sumAll(): int
    {
        return (int) $this->createQueryBuilder('d')
            ->select('COALESCE(SUM(d.amountCents), 0)')
            ->getQuery()
            ->getSingleScalarResult();
    }

    /**
     * Admin listing, newest first.
     *
     * @return array{donations: list<Donation>, total: int}
     */
    public function page(int $page, int $perPage): array
    {
        return [
            'donations' => $this->findBy([], ['createdAt' => 'DESC', 'id' => 'DESC'], $perPage, max(0, ($page - 1) * $perPage)),
            'total' => $this->countAll(),
        ];
    }

    /** Distinct linked accounts among donations (severed links excluded). */
    public function countDistinctDonors(): int
    {
        return (int) $this->createQueryBuilder('d')
            ->select('COUNT(DISTINCT d.user)')
            ->andWhere('d.user IS NOT NULL')
            ->getQuery()
            ->getSingleScalarResult();
    }

    /** Donations without any account link (anonymous, or account deleted since). */
    public function countAnonymous(): int
    {
        return $this->count(['user' => null]);
    }

    /**
     * Continuous per-day totals (cents) over the trailing window, zero-filled,
     * oldest day first. Grouped in PHP: SQL date truncation is not portable
     * between Postgres and the SQLite used by unit tests, and the row volume
     * (donations of the window) stays tiny.
     *
     * @return array<string, int> 'Y-m-d' => cents
     */
    public function dailyTotals(int $days): array
    {
        $from = new \DateTimeImmutable(sprintf('-%d days midnight', max(0, $days - 1)));
        $rows = $this->createQueryBuilder('d')
            ->select('d.createdAt AS createdAt', 'd.amountCents AS cents')
            ->andWhere('d.createdAt >= :from')
            ->setParameter('from', $from)
            ->getQuery()
            ->getArrayResult();

        $series = [];
        for ($i = 0; $i < $days; $i++) {
            $series[$from->modify(sprintf('+%d days', $i))->format('Y-m-d')] = 0;
        }
        foreach ($rows as $row) {
            $day = $row['createdAt']->format('Y-m-d');
            if (isset($series[$day])) {
                $series[$day] += (int) $row['cents'];
            }
        }

        return $series;
    }
}
