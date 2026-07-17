<?php
declare(strict_types=1);

namespace App\Entity;

use App\Repository\BuildVoteRepository;
use Doctrine\DBAL\Types\Types;
use Doctrine\ORM\Mapping as ORM;

/**
 * One user's verdict on a public build: +1 or -1, at most one row per
 * (build, voter) — enforced by a DB unique constraint, upserted by
 * {@see BuildVoteRepository::applyVote()} (re-voting the same value removes
 * the row: toggle semantics).
 *
 * Deliberately UNIDIRECTIONAL towards Build and User: neither owning entity
 * knows about votes, so the aggregate stays a pure SQL concern (SUM per build)
 * and Build/User require no mapping change. Rows follow their build and their
 * voter to the grave (CASCADE both sides).
 */
#[ORM\Entity(repositoryClass: BuildVoteRepository::class)]
#[ORM\Table(name: 'build_votes')]
#[ORM\UniqueConstraint(name: 'uniq_build_votes_build_voter', columns: ['build_id', 'voter_id'])]
final class BuildVote
{
    public const UP = 1;
    public const DOWN = -1;

    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column]
    private ?int $id = null;

    #[ORM\ManyToOne(targetEntity: Build::class)]
    #[ORM\JoinColumn(nullable: false, onDelete: 'CASCADE')]
    private Build $build;

    #[ORM\ManyToOne(targetEntity: User::class)]
    #[ORM\JoinColumn(nullable: false, onDelete: 'CASCADE')]
    private User $voter;

    #[ORM\Column(type: Types::SMALLINT)]
    private int $value;

    #[ORM\Column]
    private \DateTimeImmutable $createdAt;

    public function __construct(Build $build, User $voter, int $value)
    {
        $this->build = $build;
        $this->voter = $voter;
        $this->value = $value;
        $this->createdAt = new \DateTimeImmutable();
    }

    public function getId(): ?int
    {
        return $this->id;
    }

    public function getBuild(): Build
    {
        return $this->build;
    }

    public function getVoter(): User
    {
        return $this->voter;
    }

    public function getValue(): int
    {
        return $this->value;
    }

    /** Changing one's mind replaces the verdict in place (same row, new value). */
    public function setValue(int $value): static
    {
        $this->value = $value;

        return $this;
    }

    public function getCreatedAt(): \DateTimeImmutable
    {
        return $this->createdAt;
    }
}
