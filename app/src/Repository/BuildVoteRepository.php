<?php
declare(strict_types=1);

namespace App\Repository;

use App\Entity\Build;
use App\Entity\BuildVote;
use App\Entity\User;
use App\Service\Community\TrendsFilter;
use Doctrine\Bundle\DoctrineBundle\Repository\ServiceEntityRepository;
use Doctrine\ORM\Query\Expr\Join;
use Doctrine\ORM\QueryBuilder;
use Doctrine\Persistence\ManagerRegistry;

/**
 * Votes and the net-score aggregates built on them. The score is never stored:
 * SUM(value) is the single source of truth, so a deleted build/voter (CASCADE)
 * self-corrects every ranking without bookkeeping.
 *
 * @extends ServiceEntityRepository<BuildVote>
 */
final class BuildVoteRepository extends ServiceEntityRepository
{
    public function __construct(ManagerRegistry $registry)
    {
        parent::__construct($registry, BuildVote::class);
    }

    /**
     * Net score per build, one aggregated query. Builds without votes are
     * simply absent from the map — callers default to 0.
     *
     * @param list<int> $buildIds
     * @return array<int, int> build id => SUM(value)
     */
    public function scoreFor(array $buildIds): array
    {
        if ($buildIds === []) {
            return [];
        }

        $rows = $this->createQueryBuilder('v')
            ->select('IDENTITY(v.build) AS buildId', 'SUM(v.value) AS score')
            ->andWhere('v.build IN (:ids)')
            ->setParameter('ids', $buildIds)
            ->groupBy('v.build')
            ->getQuery()
            ->getArrayResult();

        return array_column(array_map(
            static fn (array $row): array => ['id' => (int) $row['buildId'], 'score' => (int) $row['score']],
            $rows,
        ), 'score', 'id');
    }

    public function findOneByBuildAndVoter(Build $build, User $voter): ?BuildVote
    {
        return $this->findOneBy(['build' => $build, 'voter' => $voter]);
    }

    /**
     * The voter's current verdict on each listed build (for "my vote" UI state).
     *
     * @param list<int> $buildIds
     * @return array<int, int> build id => +1|-1
     */
    public function valuesFor(User $voter, array $buildIds): array
    {
        if ($buildIds === []) {
            return [];
        }

        $rows = $this->createQueryBuilder('v')
            ->select('IDENTITY(v.build) AS buildId', 'v.value AS value')
            ->andWhere('v.voter = :voter')
            ->andWhere('v.build IN (:ids)')
            ->setParameter('voter', $voter)
            ->setParameter('ids', $buildIds)
            ->getQuery()
            ->getArrayResult();

        return array_column(array_map(
            static fn (array $row): array => ['id' => (int) $row['buildId'], 'value' => (int) $row['value']],
            $rows,
        ), 'value', 'id');
    }

    /**
     * Upsert with toggle semantics: no prior vote inserts, a different value
     * replaces (mind changed), the same value removes the row (vote withdrawn).
     * A concurrent duplicate insert is stopped by the DB unique constraint —
     * rare enough that the resulting 500 (and user retry) beats locking.
     */
    public function applyVote(Build $build, User $voter, int $value): void
    {
        if (!\in_array($value, [BuildVote::UP, BuildVote::DOWN], true)) {
            throw new \InvalidArgumentException(sprintf('Vote value must be +1 or -1, got %d.', $value));
        }

        $entityManager = $this->getEntityManager();
        $existing = $this->findOneByBuildAndVoter($build, $voter);

        if ($existing === null) {
            $entityManager->persist(new BuildVote($build, $voter, $value));
        } elseif ($existing->getValue() === $value) {
            $entityManager->remove($existing);
        } else {
            $existing->setValue($value);
        }

        $entityManager->flush();
    }

    /**
     * Public builds ranked by net score (LEFT JOIN: zero-vote builds appear),
     * ties broken by newest first, then id for a stable page split.
     *
     * @return array{builds: list<Build>, total: int}
     */
    public function topPublicBuilds(TrendsFilter $filter, int $page, int $perPage): array
    {
        $qb = $this->publicBuildsQb($filter)
            ->select('b', 'COALESCE(SUM(v.value), 0) AS HIDDEN score')
            ->leftJoin(BuildVote::class, 'v', Join::WITH, 'v.build = b')
            ->groupBy('b.id')
            ->orderBy('score', 'DESC')
            ->addOrderBy('b.createdAt', 'DESC')
            ->addOrderBy('b.id', 'DESC')
            ->setFirstResult(($page - 1) * $perPage)
            ->setMaxResults($perPage);

        return [
            'builds' => $qb->getQuery()->getResult(),
            'total' => $this->countPublic($filter),
        ];
    }

    private function countPublic(TrendsFilter $filter): int
    {
        return (int) $this->publicBuildsQb($filter)
            ->select('COUNT(b.id)')
            ->getQuery()
            ->getSingleScalarResult();
    }

    /** Shared FROM/WHERE of the trends queries (list + count must always agree). */
    private function publicBuildsQb(TrendsFilter $filter): QueryBuilder
    {
        // Banned owners vanish from every public ranking; their capability URL
        // /b/{token} deliberately keeps working (unlisted, token = the key).
        $qb = $this->getEntityManager()->createQueryBuilder()
            ->from(Build::class, 'b')
            ->join('b.owner', 'o')
            ->andWhere('b.isPublic = :isPublic')
            ->andWhere('o.isBanned = :ownerBanned')
            ->setParameter('isPublic', true)
            ->setParameter('ownerBanned', false);

        if ($filter->championId !== null) {
            $qb->andWhere('b.championId = :championId')->setParameter('championId', $filter->championId);
        }
        if ($filter->mode !== null) {
            $qb->andWhere('b.gameMode = :mode')->setParameter('mode', $filter->mode);
        }
        if ($filter->language !== null) {
            $qb->andWhere('b.language = :language')->setParameter('language', $filter->language);
        }

        return $qb;
    }
}
