<?php
declare(strict_types=1);

namespace App\Entity;

use App\Dto\ContactSubmission;
use App\Repository\ContactMessageRepository;
use Doctrine\DBAL\Types\Types;
use Doctrine\ORM\Mapping as ORM;

/**
 * A footer contact submission (bug report, feedback, review or sales enquiry).
 * Written once from a {@see ContactSubmission}; the only mutation afterwards is
 * the operator triage state ({@see markHandled()}/{@see reopen()}).
 *
 * User data (not Data Dragon), so Postgres is the right home — same class as
 * donations/accounts. The optional account link is severable (SET NULL) so a
 * message outlives the deletion of its sender's account.
 */
#[ORM\Entity(repositoryClass: ContactMessageRepository::class)]
#[ORM\Table(name: 'contact_messages')]
#[ORM\Index(columns: ['status'])]
#[ORM\Index(columns: ['created_at'])]
final class ContactMessage
{
    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column]
    private ?int $id = null;

    #[ORM\Column(enumType: ContactCategory::class)]
    private ContactCategory $category;

    #[ORM\Column(length: 120, nullable: true)]
    private ?string $name;

    #[ORM\Column(length: 255)]
    private string $email;

    #[ORM\Column(length: 160, nullable: true)]
    private ?string $subject;

    #[ORM\Column(type: Types::TEXT)]
    private string $message;

    #[ORM\Column(length: 16, nullable: true)]
    private ?string $locale;

    /** Origin IP for abuse triage only (audit journal already stores IPs at the same sensitivity). */
    #[ORM\Column(length: 64, nullable: true)]
    private ?string $ip;

    #[ORM\Column(enumType: ContactStatus::class, options: ['default' => ContactStatus::New->value])]
    private ContactStatus $status;

    #[ORM\ManyToOne(targetEntity: User::class)]
    #[ORM\JoinColumn(nullable: true, onDelete: 'SET NULL')]
    private ?User $user;

    #[ORM\Column]
    private \DateTimeImmutable $createdAt;

    #[ORM\Column(nullable: true)]
    private ?\DateTimeImmutable $handledAt = null;

    public function __construct(ContactSubmission $submission, ?string $ip = null, ?User $user = null)
    {
        $this->category = $submission->category
            ?? throw new \InvalidArgumentException('ContactSubmission must be validated (non-null category) before persistence.');
        $this->email = $submission->email;
        $this->message = $submission->message;
        $this->name = $submission->name;
        $this->subject = $submission->subject;
        $this->locale = $submission->locale;
        $this->ip = $ip;
        $this->user = $user;
        $this->status = ContactStatus::New;
        $this->createdAt = new \DateTimeImmutable();
    }

    public function markHandled(): void
    {
        $this->status = ContactStatus::Handled;
        $this->handledAt = new \DateTimeImmutable();
    }

    public function reopen(): void
    {
        $this->status = ContactStatus::New;
        $this->handledAt = null;
    }

    public function getId(): ?int
    {
        return $this->id;
    }

    public function getCategory(): ContactCategory
    {
        return $this->category;
    }

    public function getName(): ?string
    {
        return $this->name;
    }

    public function getEmail(): string
    {
        return $this->email;
    }

    public function getSubject(): ?string
    {
        return $this->subject;
    }

    public function getMessage(): string
    {
        return $this->message;
    }

    public function getLocale(): ?string
    {
        return $this->locale;
    }

    public function getIp(): ?string
    {
        return $this->ip;
    }

    public function getStatus(): ContactStatus
    {
        return $this->status;
    }

    public function isHandled(): bool
    {
        return $this->status === ContactStatus::Handled;
    }

    public function getUser(): ?User
    {
        return $this->user;
    }

    public function getCreatedAt(): \DateTimeImmutable
    {
        return $this->createdAt;
    }

    public function getHandledAt(): ?\DateTimeImmutable
    {
        return $this->handledAt;
    }
}
