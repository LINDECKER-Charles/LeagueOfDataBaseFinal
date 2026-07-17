<?php
declare(strict_types=1);

namespace App\Repository;

use App\Entity\Build;
use App\Entity\User;
use Doctrine\Bundle\DoctrineBundle\Repository\ServiceEntityRepository;
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
}
