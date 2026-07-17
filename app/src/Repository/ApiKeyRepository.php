<?php
declare(strict_types=1);

namespace App\Repository;

use App\Entity\ApiKey;
use App\Entity\ApiPlan;
use App\Entity\ApiUsage;
use App\Entity\User;
use Doctrine\Bundle\DoctrineBundle\Repository\ServiceEntityRepository;
use Doctrine\DBAL\Types\Types;
use Doctrine\Persistence\ManagerRegistry;

/**
 * @extends ServiceEntityRepository<ApiKey>
 */
final class ApiKeyRepository extends ServiceEntityRepository
{
    public function __construct(ManagerRegistry $registry)
    {
        parent::__construct($registry, ApiKey::class);
    }

    /** Single-active-key policy (v1): at most one row matches; newest wins defensively. */
    public function findActiveByUser(User $user): ?ApiKey
    {
        return $this->findOneBy(['user' => $user, 'isActive' => true], ['id' => 'DESC']);
    }

    public function findOneActiveByStripeCustomer(string $customerId): ?ApiKey
    {
        return $this->findOneBy(['stripeCustomerId' => $customerId, 'isActive' => true], ['id' => 'DESC']);
    }

    public function findOneActiveByStripeSubscription(string $subscriptionId): ?ApiKey
    {
        return $this->findOneBy(['stripeSubscriptionId' => $subscriptionId, 'isActive' => true], ['id' => 'DESC']);
    }

    /**
     * Month-to-date requests of the key, same calendar-month window as the
     * go-api quota check (day >= first of month) so both sides always agree.
     */
    public function sumRequestsForMonth(ApiKey $key, \DateTimeImmutable $when): int
    {
        $from = $when->modify('first day of this month')->setTime(0, 0);

        $sum = $this->getEntityManager()->createQueryBuilder()
            ->select('COALESCE(SUM(u.requests), 0)')
            ->from(ApiUsage::class, 'u')
            ->where('u.apiKey = :key AND u.day >= :from AND u.day < :to')
            ->setParameter('key', $key)
            ->setParameter('from', $from, Types::DATE_IMMUTABLE)
            ->setParameter('to', $from->modify('+1 month'), Types::DATE_IMMUTABLE)
            ->getQuery()
            ->getSingleScalarResult();

        return (int) $sum;
    }

    /**
     * Metered days of the trailing window, most recent first. Days without
     * traffic have no row — the portal renders only what was actually metered.
     *
     * @return list<ApiUsage>
     */
    public function recentDailyUsage(ApiKey $key, int $days): array
    {
        $from = new \DateTimeImmutable(sprintf('-%d days midnight', $days));

        return $this->getEntityManager()->createQueryBuilder()
            ->select('u')
            ->from(ApiUsage::class, 'u')
            ->where('u.apiKey = :key AND u.day >= :from')
            ->setParameter('key', $key)
            ->setParameter('from', $from, Types::DATE_IMMUTABLE)
            ->orderBy('u.day', 'DESC')
            ->getQuery()
            ->getResult();
    }

    /**
     * Re-points the metered days of a revoked key onto its replacement, so
     * "regenerate" cannot reset the month-to-date quota (usage follows the key
     * lineage, exactly like plan and credits). Safe against the (api_key_id,
     * day) unique constraint because a freshly issued key has no rows yet; a
     * go-api flush landing on the old id right after (≤60 s of cache) stays
     * behind on the revoked key — accepted undercount, same class as the Go's
     * own counter staleness.
     */
    public function transferUsage(ApiKey $from, ApiKey $to): void
    {
        $this->getEntityManager()->getConnection()->executeStatement(
            'UPDATE api_usage SET api_key_id = :to WHERE api_key_id = :from',
            ['to' => $to->getId(), 'from' => $from->getId()],
        );
    }

    /**
     * Admin listing (revoked keys included — the audit trail matters), newest first.
     *
     * @return array{keys: list<ApiKey>, total: int}
     */
    public function page(int $page, int $perPage): array
    {
        return [
            'keys' => $this->findBy([], ['createdAt' => 'DESC', 'id' => 'DESC'], $perPage, max(0, ($page - 1) * $perPage)),
            'total' => $this->count([]),
        ];
    }

    public function countActive(): int
    {
        return $this->count(['isActive' => true]);
    }

    /** Prepaid requests still owed to customers, active keys only. */
    public function sumActiveCredits(): int
    {
        return (int) $this->createQueryBuilder('k')
            ->select('COALESCE(SUM(k.creditsBalance), 0)')
            ->andWhere('k.isActive = :active')
            ->setParameter('active', true)
            ->getQuery()
            ->getSingleScalarResult();
    }

    /** @return array<string, int> plan value => active key count */
    public function countActiveByPlan(): array
    {
        $rows = $this->createQueryBuilder('k')
            ->select('k.plan AS plan', 'COUNT(k.id) AS n')
            ->andWhere('k.isActive = :active')
            ->setParameter('active', true)
            ->groupBy('k.plan')
            ->getQuery()
            ->getArrayResult();

        $byPlan = [];
        foreach ($rows as $row) {
            $plan = $row['plan'] instanceof ApiPlan ? $row['plan']->value : (string) $row['plan'];
            $byPlan[$plan] = (int) $row['n'];
        }

        return $byPlan;
    }

    /** Requests metered on/after $from, across every key (revoked included). */
    public function sumRequestsSince(\DateTimeImmutable $from): int
    {
        return (int) $this->getEntityManager()->createQueryBuilder()
            ->select('COALESCE(SUM(u.requests), 0)')
            ->from(ApiUsage::class, 'u')
            ->where('u.day >= :from')
            ->setParameter('from', $from, Types::DATE_IMMUTABLE)
            ->getQuery()
            ->getSingleScalarResult();
    }

    /**
     * Batched per-key usage since $from — one aggregate for a whole listing
     * page (the per-key {@see sumRequestsForMonth} would N+1 here).
     *
     * @param list<int> $keyIds
     * @return array<int, int> key id => requests
     */
    public function usageByKeySince(array $keyIds, \DateTimeImmutable $from): array
    {
        if ($keyIds === []) {
            return [];
        }

        $rows = $this->getEntityManager()->createQueryBuilder()
            ->select('IDENTITY(u.apiKey) AS keyId', 'SUM(u.requests) AS requests')
            ->from(ApiUsage::class, 'u')
            ->where('u.apiKey IN (:ids) AND u.day >= :from')
            ->setParameter('ids', $keyIds)
            ->setParameter('from', $from, Types::DATE_IMMUTABLE)
            ->groupBy('u.apiKey')
            ->getQuery()
            ->getArrayResult();

        $usage = [];
        foreach ($rows as $row) {
            $usage[(int) $row['keyId']] = (int) $row['requests'];
        }

        return $usage;
    }

    /**
     * Heaviest consumers of the trailing window, revoked keys included
     * (traffic history outlives revocation).
     *
     * @return list<array{keyPrefix: string, username: string, requests: int}>
     */
    public function topConsumersSince(\DateTimeImmutable $from, int $limit): array
    {
        $rows = $this->getEntityManager()->createQueryBuilder()
            ->select('k.keyPrefix AS keyPrefix', 'usr.username AS username', 'SUM(u.requests) AS requests')
            ->from(ApiUsage::class, 'u')
            ->join('u.apiKey', 'k')
            ->join('k.user', 'usr')
            ->where('u.day >= :from')
            ->setParameter('from', $from, Types::DATE_IMMUTABLE)
            ->groupBy('k.keyPrefix', 'usr.username')
            ->orderBy('requests', 'DESC')
            ->setMaxResults($limit)
            ->getQuery()
            ->getArrayResult();

        return array_map(static fn (array $row): array => [
            'keyPrefix' => (string) $row['keyPrefix'],
            'username' => (string) $row['username'],
            'requests' => (int) $row['requests'],
        ], $rows);
    }

    /**
     * Atomic credit top-up: plain SQL increment so concurrent Stripe deliveries
     * and go-api synchronous decrements can never lose an update. The rate
     * floor rides along (product rule: credits entitle at least 60 req/min).
     * The in-memory entity is NOT refreshed — callers must not read the balance
     * from the managed object after this.
     */
    public function addCredits(ApiKey $key, int $requests, int $rateFloor): void
    {
        $this->getEntityManager()->getConnection()->executeStatement(
            'UPDATE api_keys
                SET credits_balance = credits_balance + :requests,
                    rate_limit_per_min = GREATEST(rate_limit_per_min, :floor)
              WHERE id = :id',
            ['requests' => $requests, 'floor' => $rateFloor, 'id' => $key->getId()],
        );
    }
}
