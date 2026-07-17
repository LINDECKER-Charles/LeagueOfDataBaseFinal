<?php
declare(strict_types=1);

namespace App\Entity;

use App\Repository\ApiKeyRepository;
use Doctrine\DBAL\Types\Types;
use Doctrine\ORM\Mapping as ORM;

/**
 * API key of the paid public API. Column names are a CONTRACT with the go-api
 * service (go-api/schema.sql reads them nominatively) — never rename them.
 * Only the SHA-256 of the raw key is stored; the raw value is shown once at
 * creation time and never persisted.
 *
 * The relation to User is deliberately UNIDIRECTIONAL: User must not grow an
 * inverse collection (single-active-key policy lives in queries, not in the
 * aggregate). One user has at most one active key (v1); plan, quota and
 * credits live on the key because go-api authenticates keys, not users.
 */
#[ORM\Entity(repositoryClass: ApiKeyRepository::class)]
#[ORM\Table(name: 'api_keys')]
final class ApiKey
{
    public const NAME_MAX_LENGTH = 64;

    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column]
    private ?int $id = null;

    #[ORM\ManyToOne(targetEntity: User::class)]
    #[ORM\JoinColumn(name: 'user_id', nullable: false, onDelete: 'CASCADE')]
    private User $user;

    #[ORM\Column(length: self::NAME_MAX_LENGTH)]
    private string $name;

    /** SHA-256 hex digest of the full raw key (prefix included). */
    #[ORM\Column(length: 64, unique: true)]
    private string $keyHash;

    /** Displayable identification prefix, e.g. "lodb_a1b2c3d". */
    #[ORM\Column(length: 12)]
    private string $keyPrefix;

    #[ORM\Column(length: 16, enumType: ApiPlan::class, options: ['default' => 'free'])]
    private ApiPlan $plan = ApiPlan::Free;

    #[ORM\Column(options: ['default' => ApiPlan::QUOTA_FREE])]
    private int $monthlyQuota = ApiPlan::QUOTA_FREE;

    /** Prepaid requests, spent by go-api after the monthly quota is exhausted. */
    #[ORM\Column(type: Types::BIGINT, options: ['default' => 0])]
    private int $creditsBalance = 0;

    #[ORM\Column(options: ['default' => ApiPlan::RATE_FREE])]
    private int $rateLimitPerMin = ApiPlan::RATE_FREE;

    #[ORM\Column(options: ['default' => true])]
    private bool $isActive = true;

    #[ORM\Column(type: Types::DATETIMETZ_IMMUTABLE)]
    private \DateTimeImmutable $createdAt;

    #[ORM\Column(type: Types::DATETIMETZ_IMMUTABLE, nullable: true)]
    private ?\DateTimeImmutable $revokedAt = null;

    #[ORM\Column(length: 64, nullable: true)]
    private ?string $stripeCustomerId = null;

    #[ORM\Column(length: 64, nullable: true)]
    private ?string $stripeSubscriptionId = null;

    public function __construct(User $user, string $name, string $keyHash, string $keyPrefix)
    {
        $this->user = $user;
        $this->name = $name;
        $this->keyHash = $keyHash;
        $this->keyPrefix = $keyPrefix;
        $this->createdAt = new \DateTimeImmutable();
    }

    public function getId(): ?int
    {
        return $this->id;
    }

    public function getUser(): User
    {
        return $this->user;
    }

    public function getName(): string
    {
        return $this->name;
    }

    public function getKeyHash(): string
    {
        return $this->keyHash;
    }

    public function getKeyPrefix(): string
    {
        return $this->keyPrefix;
    }

    public function getPlan(): ApiPlan
    {
        return $this->plan;
    }

    public function getMonthlyQuota(): int
    {
        return $this->monthlyQuota;
    }

    public function getCreditsBalance(): int
    {
        return $this->creditsBalance;
    }

    public function getRateLimitPerMin(): int
    {
        return $this->rateLimitPerMin;
    }

    public function isActive(): bool
    {
        return $this->isActive;
    }

    public function getCreatedAt(): \DateTimeImmutable
    {
        return $this->createdAt;
    }

    public function getRevokedAt(): ?\DateTimeImmutable
    {
        return $this->revokedAt;
    }

    public function getStripeCustomerId(): ?string
    {
        return $this->stripeCustomerId;
    }

    public function getStripeSubscriptionId(): ?string
    {
        return $this->stripeSubscriptionId;
    }

    public function hasSubscription(): bool
    {
        return $this->stripeSubscriptionId !== null;
    }

    /** Applies a paid plan: quota and rate follow the plan policy (credits floor kept). */
    public function applyPlan(ApiPlan $plan): void
    {
        $this->plan = $plan;
        $this->monthlyQuota = $plan->monthlyQuota();
        $this->rateLimitPerMin = $this->withCreditsRateFloor($plan->rateLimitPerMin());
    }

    /**
     * Subscription ended: back to the free plan. Prepaid credits survive, and
     * keep their 60/min entitlement (product rule "rate 60 dès crédits > 0"),
     * so the rate only falls back to the free 10/min on a creditless key.
     */
    public function downgradeToFree(): void
    {
        $this->plan = ApiPlan::Free;
        $this->monthlyQuota = ApiPlan::QUOTA_FREE;
        $this->rateLimitPerMin = $this->withCreditsRateFloor(ApiPlan::RATE_FREE);
        // Customer id is kept as an ops trail; only the dead subscription is detached.
        $this->stripeSubscriptionId = null;
    }

    public function attachStripe(?string $customerId, ?string $subscriptionId): void
    {
        $this->stripeCustomerId = $customerId;
        $this->stripeSubscriptionId = $subscriptionId;
    }

    public function revoke(): void
    {
        $this->isActive = false;
        $this->revokedAt = new \DateTimeImmutable();
    }

    /**
     * "Regenerate" rotates the secret, never the entitlements: the replacement
     * key inherits plan, quota, credits, rate and Stripe ids from the revoked one.
     */
    public function carryEntitlementsFrom(self $previous): void
    {
        $this->plan = $previous->plan;
        $this->monthlyQuota = $previous->monthlyQuota;
        $this->creditsBalance = $previous->creditsBalance;
        $this->rateLimitPerMin = $previous->rateLimitPerMin;
        $this->stripeCustomerId = $previous->stripeCustomerId;
        $this->stripeSubscriptionId = $previous->stripeSubscriptionId;
    }

    private function withCreditsRateFloor(int $planRate): int
    {
        return $this->creditsBalance > 0 ? max($planRate, ApiPlan::RATE_CREDITS) : $planRate;
    }
}
