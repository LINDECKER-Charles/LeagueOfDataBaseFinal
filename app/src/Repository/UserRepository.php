<?php
declare(strict_types=1);

namespace App\Repository;

use App\Entity\User;
use Doctrine\Bundle\DoctrineBundle\Repository\ServiceEntityRepository;
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
