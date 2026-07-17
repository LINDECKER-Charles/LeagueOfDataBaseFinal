<?php
declare(strict_types=1);

namespace App\Entity;

use App\Repository\BuildRepository;
use Doctrine\DBAL\Types\Types;
use Doctrine\ORM\Mapping as ORM;

/**
 * User-authored champion build, written against a given game patch.
 *
 * Invariant shapes (stored as JSONB, produced/consumed by the build editor):
 *  - runes: {"primaryStyleId": int, "primarySelections": int[4],
 *            "secondaryStyleId": int, "secondarySelections": int[2]}
 *  - steps: [{"label": string, "note": string|null, "items": string[]}]
 *
 * The share token is URL-safe, random and unique: it is the only key to a
 * build's public page (/build/{token}), independent of the numeric id.
 */
#[ORM\Entity(repositoryClass: BuildRepository::class)]
#[ORM\Table(name: 'builds')]
#[ORM\HasLifecycleCallbacks]
final class Build
{
    private const SHARE_TOKEN_BYTES = 12;

    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column]
    private ?int $id = null;

    #[ORM\ManyToOne(targetEntity: User::class, inversedBy: 'builds')]
    #[ORM\JoinColumn(nullable: false, onDelete: 'CASCADE')]
    private ?User $owner = null;

    #[ORM\Column(length: 80)]
    private string $name = '';

    #[ORM\Column(length: 64)]
    private string $championId = '';

    /** Patch the build was written for (e.g. "15.14.1"). */
    #[ORM\Column(length: 24)]
    private string $gameVersion = '';

    #[ORM\Column(type: Types::TEXT, nullable: true)]
    private ?string $description = null;

    /** @var array<string, mixed> See the runes shape invariant in the class docblock. */
    #[ORM\Column(type: Types::JSON, options: ['jsonb' => true])]
    private array $runes = [];

    /** @var list<array<string, mixed>> See the steps shape invariant in the class docblock. */
    #[ORM\Column(type: Types::JSON, options: ['jsonb' => true])]
    private array $steps = [];

    #[ORM\Column(options: ['default' => false])]
    private bool $isPublic = false;

    #[ORM\Column(length: 24, unique: true)]
    private string $shareToken;

    #[ORM\Column]
    private \DateTimeImmutable $createdAt;

    #[ORM\Column]
    private \DateTimeImmutable $updatedAt;

    public function __construct()
    {
        $this->shareToken = self::generateShareToken();
        $this->createdAt = new \DateTimeImmutable();
        $this->updatedAt = $this->createdAt;
    }

    public function getId(): ?int
    {
        return $this->id;
    }

    public function getOwner(): ?User
    {
        return $this->owner;
    }

    public function setOwner(?User $owner): static
    {
        $this->owner = $owner;

        return $this;
    }

    public function getName(): string
    {
        return $this->name;
    }

    public function setName(string $name): static
    {
        $this->name = $name;

        return $this;
    }

    public function getChampionId(): string
    {
        return $this->championId;
    }

    public function setChampionId(string $championId): static
    {
        $this->championId = $championId;

        return $this;
    }

    public function getGameVersion(): string
    {
        return $this->gameVersion;
    }

    public function setGameVersion(string $gameVersion): static
    {
        $this->gameVersion = $gameVersion;

        return $this;
    }

    public function getDescription(): ?string
    {
        return $this->description;
    }

    public function setDescription(?string $description): static
    {
        $this->description = $description;

        return $this;
    }

    /** @return array<string, mixed> */
    public function getRunes(): array
    {
        return $this->runes;
    }

    /** @param array<string, mixed> $runes */
    public function setRunes(array $runes): static
    {
        $this->runes = $runes;

        return $this;
    }

    /** @return list<array<string, mixed>> */
    public function getSteps(): array
    {
        return $this->steps;
    }

    /** @param list<array<string, mixed>> $steps */
    public function setSteps(array $steps): static
    {
        $this->steps = $steps;

        return $this;
    }

    public function isPublic(): bool
    {
        return $this->isPublic;
    }

    public function setIsPublic(bool $isPublic): static
    {
        $this->isPublic = $isPublic;

        return $this;
    }

    public function getShareToken(): string
    {
        return $this->shareToken;
    }

    /** Invalidate every previously shared URL by minting a fresh token. */
    public function regenerateShareToken(): static
    {
        $this->shareToken = self::generateShareToken();

        return $this;
    }

    public function getCreatedAt(): \DateTimeImmutable
    {
        return $this->createdAt;
    }

    public function getUpdatedAt(): \DateTimeImmutable
    {
        return $this->updatedAt;
    }

    #[ORM\PreUpdate]
    public function touchUpdatedAt(): void
    {
        $this->updatedAt = new \DateTimeImmutable();
    }

    private static function generateShareToken(): string
    {
        // 12 random bytes hex-encoded -> 24 URL-safe chars (fits the column).
        return bin2hex(random_bytes(self::SHARE_TOKEN_BYTES));
    }
}
