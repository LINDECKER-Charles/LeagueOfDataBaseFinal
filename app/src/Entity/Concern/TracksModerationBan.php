<?php
declare(strict_types=1);

namespace App\Entity\Concern;

use Doctrine\DBAL\Types\Types;
use Doctrine\ORM\Mapping as ORM;

/**
 * Moderation ban state of an account, with its two transitions. The three
 * columns are only ever written together by {@see self::ban()} / {@see
 * self::unban()} — the flag must never diverge from its timestamp and reason.
 */
trait TracksModerationBan
{
    public const BAN_REASON_MAX_LENGTH = 255;

    /** Moderation ban: blocks login (UserChecker) and hides the public surfaces. */
    #[ORM\Column(options: ['default' => false])]
    private bool $isBanned = false;

    #[ORM\Column(type: Types::DATETIMETZ_IMMUTABLE, nullable: true)]
    private ?\DateTimeImmutable $bannedAt = null;

    /** Operator-facing note, never shown to the banned player. */
    #[ORM\Column(length: self::BAN_REASON_MAX_LENGTH, nullable: true)]
    private ?string $banReason = null;

    public function isBanned(): bool
    {
        return $this->isBanned;
    }

    public function getBannedAt(): ?\DateTimeImmutable
    {
        return $this->bannedAt;
    }

    public function getBanReason(): ?string
    {
        return $this->banReason;
    }

    public function ban(?string $reason = null): void
    {
        $this->isBanned = true;
        $this->bannedAt = new \DateTimeImmutable();
        $this->banReason = $reason === null
            ? null
            : mb_substr($reason, 0, self::BAN_REASON_MAX_LENGTH);
    }

    public function unban(): void
    {
        $this->isBanned = false;
        $this->bannedAt = null;
        $this->banReason = null;
    }
}
