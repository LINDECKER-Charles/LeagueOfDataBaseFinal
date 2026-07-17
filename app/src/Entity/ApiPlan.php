<?php
declare(strict_types=1);

namespace App\Entity;

/**
 * Billing plan carried by an API key. Backed values are the exact strings
 * stored in api_keys.plan and consumed by the go-api service (contract:
 * go-api/schema.sql — do not rename).
 *
 * Quotas are per CALENDAR MONTH even for annual plans: go-api enforces usage
 * against monthly_quota over the current month, so yearly volumes are
 * expressed here as their monthly slice (240k/an -> 20k/mois, 720k -> 60k).
 */
enum ApiPlan: string
{
    case Free = 'free';
    case Credits = 'credits';
    case Monthly = 'monthly';
    case MonthlyPlus = 'monthly_plus';
    case Annual = 'annual';
    case AnnualPlus = 'annual_plus';

    public const QUOTA_FREE = 500;
    public const QUOTA_MONTHLY = 15_000;
    public const QUOTA_MONTHLY_PLUS = 45_000;
    public const QUOTA_ANNUAL = 20_000;
    public const QUOTA_ANNUAL_PLUS = 60_000;

    public const RATE_FREE = 10;
    /** Product rule: this rate applies as soon as prepaid credits are available. */
    public const RATE_CREDITS = 60;
    public const RATE_MONTHLY = 120;
    public const RATE_ANNUAL = 300;

    public const PRICE_MONTHLY_CENTS = 500;
    public const PRICE_MONTHLY_PLUS_CENTS = 1_500;
    public const PRICE_ANNUAL_CENTS = 4_800;
    public const PRICE_ANNUAL_PLUS_CENTS = 14_400;

    public function monthlyQuota(): int
    {
        return match ($this) {
            self::Free, self::Credits => self::QUOTA_FREE,
            self::Monthly => self::QUOTA_MONTHLY,
            self::MonthlyPlus => self::QUOTA_MONTHLY_PLUS,
            self::Annual => self::QUOTA_ANNUAL,
            self::AnnualPlus => self::QUOTA_ANNUAL_PLUS,
        };
    }

    public function rateLimitPerMin(): int
    {
        return match ($this) {
            self::Free => self::RATE_FREE,
            self::Credits => self::RATE_CREDITS,
            self::Monthly, self::MonthlyPlus => self::RATE_MONTHLY,
            self::Annual, self::AnnualPlus => self::RATE_ANNUAL,
        };
    }

    /** Subscription price in euro cents; null for the non-subscription plans. */
    public function priceCents(): ?int
    {
        return match ($this) {
            self::Monthly => self::PRICE_MONTHLY_CENTS,
            self::MonthlyPlus => self::PRICE_MONTHLY_PLUS_CENTS,
            self::Annual => self::PRICE_ANNUAL_CENTS,
            self::AnnualPlus => self::PRICE_ANNUAL_PLUS_CENTS,
            self::Free, self::Credits => null,
        };
    }

    /** Stripe `recurring.interval` of the subscription plans, null otherwise. */
    public function stripeInterval(): ?string
    {
        return match ($this) {
            self::Monthly, self::MonthlyPlus => 'month',
            self::Annual, self::AnnualPlus => 'year',
            self::Free, self::Credits => null,
        };
    }

    public function isSubscription(): bool
    {
        return $this->stripeInterval() !== null;
    }

    /** @return list<self> plans purchasable as Stripe subscriptions, in display order */
    public static function subscriptions(): array
    {
        return [self::Monthly, self::MonthlyPlus, self::Annual, self::AnnualPlus];
    }
}
