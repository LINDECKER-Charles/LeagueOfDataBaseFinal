<?php
declare(strict_types=1);

namespace App\Repository;

use App\Entity\User;
use Doctrine\Bundle\DoctrineBundle\Repository\ServiceEntityRepository;
use Doctrine\ORM\QueryBuilder;
use Doctrine\Persistence\ManagerRegistry;
use Symfony\Bridge\Doctrine\Security\User\UserLoaderInterface;
use Symfony\Component\Security\Core\Exception\UnsupportedUserException;
use Symfony\Component\Security\Core\User\PasswordAuthenticatedUserInterface;
use Symfony\Component\Security\Core\User\PasswordUpgraderInterface;

/**
 * @extends ServiceEntityRepository<User>
 */
final class UserRepository extends ServiceEntityRepository implements PasswordUpgraderInterface, UserLoaderInterface
{
    public function __construct(ManagerRegistry $registry)
    {
        parent::__construct($registry, User::class);
    }

    /**
     * Login accepts the email OR the username, case-insensitively — mirroring
     * the LOWER() unique indexes, so each side of the OR matches at most one row.
     *
     * @see UserLoaderInterface
     */
    public function loadUserByIdentifier(string $identifier): ?User
    {
        return $this->createQueryBuilder('u')
            ->andWhere('LOWER(u.email) = :identifier OR LOWER(u.username) = :identifier')
            ->setParameter('identifier', mb_strtolower($identifier))
            ->getQuery()
            ->getOneOrNullResult();
    }

    /**
     * Case-insensitive username lookup backing #[UniqueEntity(repositoryMethod)]:
     * the DB constraint is LOWER(username), so the validator must compare the same way.
     *
     * @param array{username?: string} $criteria
     * @return list<User>
     */
    public function findForUsernameUniqueness(array $criteria): array
    {
        $username = $criteria['username'] ?? '';
        if ($username === '') {
            return [];
        }

        return $this->createQueryBuilder('u')
            ->andWhere('LOWER(u.username) = :username')
            ->setParameter('username', mb_strtolower($username))
            ->getQuery()
            ->getResult();
    }

    /**
     * Public-profile lookup (/u/{username}) — case-insensitive, mirroring the
     * LOWER(username) unique index, so at most one row can match.
     */
    public function findOneByUsernameInsensitive(string $username): ?User
    {
        return $this->createQueryBuilder('u')
            ->andWhere('LOWER(u.username) = :username')
            ->setParameter('username', mb_strtolower($username))
            ->getQuery()
            ->getOneOrNullResult();
    }

    /**
     * Admin moderation listing: substring match on username OR email
     * (case-insensitive), newest accounts first, build count fetched in the
     * same query (SIZE subquery) so the page renders without N+1.
     *
     * @return array{rows: list<array{user: User, buildCount: int}>, total: int}
     */
    public function searchPaginated(string $query, int $page, int $perPage): array
    {
        $rows = $this->searchQb($query)
            ->select('u AS user', 'SIZE(u.builds) AS buildCount')
            ->orderBy('u.createdAt', 'DESC')
            ->addOrderBy('u.id', 'DESC')
            ->setFirstResult(max(0, ($page - 1) * $perPage))
            ->setMaxResults($perPage)
            ->getQuery()
            ->getResult();

        return [
            'rows' => array_map(
                static fn (array $row): array => ['user' => $row['user'], 'buildCount' => (int) $row['buildCount']],
                $rows,
            ),
            'total' => $this->countSearch($query),
        ];
    }

    public function countAll(): int
    {
        return $this->count([]);
    }

    public function countBanned(): int
    {
        return $this->count(['isBanned' => true]);
    }

    public function countSupporters(): int
    {
        return $this->count(['isSupporter' => true]);
    }

    public function countNewSince(\DateTimeImmutable $since): int
    {
        return (int) $this->createQueryBuilder('u')
            ->select('COUNT(u.id)')
            ->andWhere('u.createdAt >= :since')
            ->setParameter('since', $since)
            ->getQuery()
            ->getSingleScalarResult();
    }

    private function countSearch(string $query): int
    {
        return (int) $this->searchQb($query)
            ->select('COUNT(u.id)')
            ->getQuery()
            ->getSingleScalarResult();
    }

    /** Shared WHERE of the admin search (list + count must always agree). */
    private function searchQb(string $query): QueryBuilder
    {
        $qb = $this->createQueryBuilder('u');
        if ($query !== '') {
            // LIKE wildcards in the needle are tolerated on purpose (admin-only
            // search, a broader match at worst) — escaping them is not portable
            // between Postgres and the SQLite used by unit tests.
            $qb->andWhere('LOWER(u.username) LIKE :needle OR LOWER(u.email) LIKE :needle')
                ->setParameter('needle', '%' . mb_strtolower($query) . '%');
        }

        return $qb;
    }

    /**
     * Used to upgrade (rehash) the user's password automatically over time.
     *
     * @see PasswordUpgraderInterface
     */
    public function upgradePassword(PasswordAuthenticatedUserInterface $user, string $newHashedPassword): void
    {
        if (!$user instanceof User) {
            throw new UnsupportedUserException(sprintf('Instances of "%s" are not supported.', $user::class));
        }

        $user->setPassword($newHashedPassword);
        $this->getEntityManager()->flush();
    }
}
