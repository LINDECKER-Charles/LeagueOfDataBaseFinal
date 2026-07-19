<?php
declare(strict_types=1);

namespace App\Repository;

use App\Entity\ContactMessage;
use App\Entity\ContactStatus;
use Doctrine\Bundle\DoctrineBundle\Repository\ServiceEntityRepository;
use Doctrine\Persistence\ManagerRegistry;

/**
 * @extends ServiceEntityRepository<ContactMessage>
 */
final class ContactMessageRepository extends ServiceEntityRepository
{
    public function __construct(ManagerRegistry $registry)
    {
        parent::__construct($registry, ContactMessage::class);
    }

    public function save(ContactMessage $message): void
    {
        $em = $this->getEntityManager();
        $em->persist($message);
        $em->flush();
    }

    /**
     * Admin inbox listing, newest first, optionally scoped to a triage state.
     *
     * @return array{rows: list<ContactMessage>, total: int}
     */
    public function page(int $page, int $perPage, ?ContactStatus $status = null): array
    {
        $criteria = $status === null ? [] : ['status' => $status];

        return [
            'rows' => $this->findBy($criteria, ['createdAt' => 'DESC', 'id' => 'DESC'], $perPage, max(0, ($page - 1) * $perPage)),
            'total' => (int) $this->count($criteria),
        ];
    }

    public function countByStatus(ContactStatus $status): int
    {
        return (int) $this->count(['status' => $status]);
    }

    public function countSince(\DateTimeImmutable $since): int
    {
        return (int) $this->createQueryBuilder('c')
            ->select('COUNT(c.id)')
            ->andWhere('c.createdAt >= :since')
            ->setParameter('since', $since)
            ->getQuery()
            ->getSingleScalarResult();
    }
}
