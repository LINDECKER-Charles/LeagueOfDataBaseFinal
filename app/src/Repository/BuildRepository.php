<?php
declare(strict_types=1);

namespace App\Repository;

use App\Entity\Build;
use App\Entity\User;
use Doctrine\Bundle\DoctrineBundle\Repository\ServiceEntityRepository;
use Doctrine\ORM\QueryBuilder;
use Doctrine\Persistence\ManagerRegistry;

/**
 * @extends ServiceEntityRepository<Build>
 */
final class BuildRepository extends ServiceEntityRepository
{
    public function __construct(ManagerRegistry $registry)
    {
        parent::__construct($registry, Build::class);
    }

    /**
     * Every build of the owner (private included) — the "my builds" listing.
     *
     * @return list<Build>
     */
    public function findOwnedBy(User $owner): array
    {
        return $this->findBy(['owner' => $owner], ['updatedAt' => 'DESC']);
    }

    /**
     * Only the builds the owner chose to publish — the public profile listing.
     *
     * @return list<Build>
     */
    public function findPublicByOwner(User $owner): array
    {
        return $this->findBy(['owner' => $owner, 'isPublic' => true], ['updatedAt' => 'DESC']);
    }

    public function findOneByShareToken(string $token): ?Build
    {
        return $this->findOneBy(['shareToken' => $token]);
    }

    /**
     * Admin moderation listing: substring match on name OR champion id
     * (case-insensitive), optional visibility filter, newest first.
     *
     * @return array{builds: list<Build>, total: int}
     */
    public function searchPaginated(string $query, ?bool $isPublic, int $page, int $perPage): array
    {
        $builds = $this->moderationQb($query, $isPublic)
            ->orderBy('b.createdAt', 'DESC')
            ->addOrderBy('b.id', 'DESC')
            ->setFirstResult(max(0, ($page - 1) * $perPage))
            ->setMaxResults($perPage)
            ->getQuery()
            ->getResult();

        $total = (int) $this->moderationQb($query, $isPublic)
            ->select('COUNT(b.id)')
            ->getQuery()
            ->getSingleScalarResult();

        return ['builds' => $builds, 'total' => $total];
    }

    public function countAll(): int
    {
        return $this->count([]);
    }

    public function countPublic(): int
    {
        return $this->count(['isPublic' => true]);
    }

    /** Shared WHERE of the moderation search (list + count must always agree). */
    private function moderationQb(string $query, ?bool $isPublic): QueryBuilder
    {
        $qb = $this->createQueryBuilder('b');
        if ($query !== '') {
            // LIKE wildcards tolerated on purpose — see UserRepository::searchQb().
            $qb->andWhere('LOWER(b.name) LIKE :needle OR LOWER(b.championId) LIKE :needle')
                ->setParameter('needle', '%' . mb_strtolower($query) . '%');
        }
        if ($isPublic !== null) {
            $qb->andWhere('b.isPublic = :isPublic')->setParameter('isPublic', $isPublic);
        }

        return $qb;
    }

    /**
     * Champions having at least one public build — feeds the trends filter,
     * which must only offer choices that yield results.
     *
     * @return list<string>
     */
    public function distinctPublicChampionIds(): array
    {
        $rows = $this->createQueryBuilder('b')
            ->select('DISTINCT b.championId')
            ->andWhere('b.isPublic = :isPublic')
            ->setParameter('isPublic', true)
            ->orderBy('b.championId', 'ASC')
            ->getQuery()
            ->getSingleColumnResult();

        return array_map(strval(...), $rows);
    }
}
