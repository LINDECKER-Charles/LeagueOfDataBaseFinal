<?php
declare(strict_types=1);

namespace App\Service\Donation;

/**
 * Pure builder for the Stripe Checkout Session payload of a one-off donation.
 * Keeps the exact wire shape in one testable place, away from the API client.
 */
final class CheckoutSessionParams
{
    /**
     * Stripe substitutes this placeholder itself — it must reach the API
     * verbatim, never URL-encoded.
     */
    public const SESSION_ID_PLACEHOLDER = '{CHECKOUT_SESSION_ID}';

    private const SUBMIT_TYPE = 'donate';
    private const METADATA_SOURCE = 'lodb-donate';

    private function __construct()
    {
        // Static builder — never instantiated.
    }

    /**
     * @param int    $amountCents Validated amount (see DonationTiers)
     * @param string $productName Label shown on Stripe's hosted payment page
     * @param string $successUrl  Absolute URL, without query string
     * @param string $cancelUrl   Absolute URL
     *
     * @return array<string, mixed> Ready-to-send Checkout Session create params
     */
    public static function build(int $amountCents, string $productName, string $successUrl, string $cancelUrl): array
    {
        return [
            'mode' => 'payment',
            'submit_type' => self::SUBMIT_TYPE,
            'line_items' => [[
                'quantity' => 1,
                'price_data' => [
                    'currency' => DonationTiers::CURRENCY,
                    'unit_amount' => $amountCents,
                    'product_data' => ['name' => $productName],
                ],
            ]],
            'success_url' => $successUrl . '?session_id=' . self::SESSION_ID_PLACEHOLDER,
            'cancel_url' => $cancelUrl,
            'metadata' => ['source' => self::METADATA_SOURCE],
        ];
    }
}
