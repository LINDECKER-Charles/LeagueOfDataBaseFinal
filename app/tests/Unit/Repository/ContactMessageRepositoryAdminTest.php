<?php
declare(strict_types=1);

namespace App\Tests\Unit\Repository;

use App\Dto\ContactSubmission;
use App\Entity\ContactCategory;
use App\Entity\ContactMessage;
use App\Entity\ContactStatus;
use App\Entity\User;
use App\Repository\ContactMessageRepository;
use App\Tests\Unit\Support\InMemoryOrm;
use Doctrine\ORM\EntityManager;
use Doctrine\Persistence\ManagerRegistry;
use PHPUnit\Framework\TestCase;

/**
 * Admin contact-inbox queries against real SQL: newest-first pagination, the
 * status filter and its counts, and the trailing-window count.
 */
final class ContactMessageRepositoryAdminTest extends TestCase
{
    private EntityManager $entityManager;
    private ContactMessageRepository $messages;

    protected function setUp(): void
    {
        $this->entityManager = InMemoryOrm::entityManager([User::class, ContactMessage::class]);
        $registry = $this->createStub(ManagerRegistry::class);
        $registry->method('getManagerForClass')->willReturn($this->entityManager);
        $this->messages = new ContactMessageRepository($registry);
    }

    public function testPageIsNewestFirstAndCountsTotal(): void
    {
        $this->givenMessage('a@example.test', daysAgo: 3);
        $this->givenMessage('b@example.test', daysAgo: 1);
        $this->givenMessage('c@example.test', daysAgo: 2);

        ['rows' => $rows, 'total' => $total] = $this->messages->page(1, 2);

        self::assertSame(3, $total);
        self::assertSame(
            ['b@example.test', 'c@example.test'],
            array_map(static fn (ContactMessage $m): string => $m->getEmail(), $rows),
        );
    }

    public function testStatusFilterAndCounts(): void
    {
        $this->givenMessage('new1@example.test');
        $this->givenMessage('new2@example.test');
        $handled = $this->givenMessage('done@example.test');
        $handled->markHandled();
        $this->entityManager->flush();

        self::assertSame(2, $this->messages->countByStatus(ContactStatus::New));
        self::assertSame(1, $this->messages->countByStatus(ContactStatus::Handled));

        ['rows' => $handledRows, 'total' => $handledTotal] = $this->messages->page(1, 25, ContactStatus::Handled);
        self::assertSame(1, $handledTotal);
        self::assertSame('done@example.test', $handledRows[0]->getEmail());
        self::assertTrue($handledRows[0]->isHandled());
    }

    public function testCountSinceWindow(): void
    {
        $this->givenMessage('recent@example.test', daysAgo: 1);
        $this->givenMessage('old@example.test', daysAgo: 40);

        self::assertSame(1, $this->messages->countSince(new \DateTimeImmutable('-7 days')));
    }

    public function testMarkHandledAndReopenTogglesStatusAndTimestamp(): void
    {
        $message = $this->givenMessage('toggle@example.test');
        self::assertFalse($message->isHandled());
        self::assertNull($message->getHandledAt());

        $message->markHandled();
        self::assertTrue($message->isHandled());
        self::assertInstanceOf(\DateTimeImmutable::class, $message->getHandledAt());

        $message->reopen();
        self::assertFalse($message->isHandled());
        self::assertNull($message->getHandledAt());
    }

    private function givenMessage(string $email, int $daysAgo = 0): ContactMessage
    {
        $submission = new ContactSubmission(
            category: ContactCategory::Bug,
            email: $email,
            message: 'A message body long enough to pass validation.',
        );
        $message = new ContactMessage($submission, ip: '203.0.113.1');
        if ($daysAgo > 0) {
            new \ReflectionProperty(ContactMessage::class, 'createdAt')
                ->setValue($message, new \DateTimeImmutable("-{$daysAgo} days")->setTime(12, 0));
        }
        $this->entityManager->persist($message);
        $this->entityManager->flush();

        return $message;
    }
}
