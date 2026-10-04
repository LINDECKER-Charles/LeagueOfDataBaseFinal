<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

/**
 * One-time credit pack policy: 1 € buys 1 000 requests, three fixed sizes.
 * Credits are spent by go-api after the monthly quota and — contractual rule,
 * not enforced in v1 — are valid 12 months from purchase.
 */
enum ApiCreditPack: string
{
    case Small = 'small';
    case Medium = 'medium';
    case Large = 'large';

    public const PRICE_SMALL_CENTS = 500;
    public const PRICE_MEDIUM_CENTS = 1_000;
    public const PRICE_LARGE_CENTS = 2_000;

    /** 1 € / 1 000 requests <=> 10 requests per cent. */
    private const REQUESTS_PER_CENT = 10;

    public function priceCents(): int
    {
        return match ($this) {
            self::Small => self::PRICE_SMALL_CENTS,
            self::Medium => self::PRICE_MEDIUM_CENTS,
            self::Large => self::PRICE_LARGE_CENTS,
        };
    }

    public function requests(): int
    {
        return $this->priceCents() * self::REQUESTS_PER_CENT;
    }
}
