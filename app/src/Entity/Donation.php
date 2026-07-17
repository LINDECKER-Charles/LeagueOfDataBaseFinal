<?php
declare(strict_types=1);

namespace App\Entity;

use App\Repository\DonationRepository;
use Doctrine\ORM\Mapping as ORM;

/**
 * Completed Stripe donation, written by the webhook only — immutable once
 * recorded (no setters on purpose: an accounting line never mutates).
 *
 * The unique Stripe session id is the idempotency key: a redelivered
 * checkout.session.completed event maps to the same row and is skipped.
 * The donor link is optional and severable (SET NULL on account deletion):
 * an anonymous donation, or one whose account is gone, stays countable.
 */
#[ORM\Entity(repositoryClass: DonationRepository::class)]
#[ORM\Table(name: 'donations')]
final class Donation
{
    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column]
    private ?int $id = null;

    #[ORM\Column(length: 255, unique: true)]
    private string $stripeSessionId;

    #[ORM\ManyToOne(targetEntity: User::class)]
    #[ORM\JoinColumn(nullable: true, onDelete: 'SET NULL')]
    private ?User $user;

    #[ORM\Column]
    private int $amountCents;

    #[ORM\Column(length: 3)]
    private string $currency;

    #[ORM\Column]
    private \DateTimeImmutable $createdAt;

    public function __construct(string $stripeSessionId, int $amountCents, string $currency, ?User $user = null)
    {
        $this->stripeSessionId = $stripeSessionId;
        $this->amountCents = $amountCents;
        $this->currency = $currency;
        $this->user = $user;
        $this->createdAt = new \DateTimeImmutable();
    }

    public function getId(): ?int
    {
        return $this->id;
    }

    public function getStripeSessionId(): string
    {
        return $this->stripeSessionId;
    }

    public function getUser(): ?User
    {
        return $this->user;
    }

    public function getAmountCents(): int
    {
        return $this->amountCents;
    }

    public function getCurrency(): string
    {
        return $this->currency;
    }

    public function getCreatedAt(): \DateTimeImmutable
    {
        return $this->createdAt;
    }
}
