<?php
declare(strict_types=1);

namespace App\Service\Donation;

/**
 * Donation amount policy — the single place that knows the preset tiers, the
 * accepted bounds and how a free-text euro amount becomes Stripe cents.
 * Pure and stateless so the whole policy is unit-testable without the gateway.
 */
final class DonationTiers
{
    /** Preset tiers offered on the donate page, in cents. */
    public const PRESETS_CENTS = [300, 500, 1000, 2500];

    public const MIN_CENTS = 100;
    public const MAX_CENTS = 50000;
    public const CURRENCY = 'eur';

    private const CENTS_PER_EURO = 100;

    /**
     * Strict decimal euros: up to 2 decimals, dot or French comma separator.
     * Bounds are enforced separately so the pattern only rejects malformed input.
     */
    private const EURO_PATTERN = '/^(?<units>\d{1,6})(?:[.,](?<decimals>\d{1,2}))?$/';

    private function __construct()
    {
        // Static policy holder — never instantiated.
    }

    /**
     * Normalizes a user-typed euro amount ("5", "5.5", "5,50") into cents.
     * Integer-only math: no float rounding drift on the money path.
     *
     * @return int|null Cents within [MIN_CENTS, MAX_CENTS], or null when rejected.
     */
    public static function normalizeEuroInput(string $input): ?int
    {
        if (preg_match(self::EURO_PATTERN, trim($input), $matches) !== 1) {
            return null;
        }

        $decimals = str_pad($matches['decimals'] ?? '', 2, '0');
        $cents = ((int) $matches['units']) * self::CENTS_PER_EURO + (int) $decimals;

        return self::isWithinBounds($cents) ? $cents : null;
    }

    public static function isPreset(int $cents): bool
    {
        return \in_array($cents, self::PRESETS_CENTS, true);
    }

    public static function isWithinBounds(int $cents): bool
    {
        return $cents >= self::MIN_CENTS && $cents <= self::MAX_CENTS;
    }
}
