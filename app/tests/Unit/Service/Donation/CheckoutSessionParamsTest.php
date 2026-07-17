<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Donation;

use App\Service\Donation\CheckoutSessionParams;
use App\Service\Donation\DonationTiers;
use PHPUnit\Framework\TestCase;

final class CheckoutSessionParamsTest extends TestCase
{
    private const SUCCESS_URL = 'https://lodb.example/donate/success';
    private const CANCEL_URL = 'https://lodb.example/donate/cancel';

    public function testBuildsTheExactStripePayload(): void
    {
        $params = CheckoutSessionParams::build(1000, 'Donation', self::SUCCESS_URL, self::CANCEL_URL);

        self::assertSame([
            'mode' => 'payment',
            'submit_type' => 'donate',
            'line_items' => [[
                'quantity' => 1,
                'price_data' => [
                    'currency' => 'eur',
                    'unit_amount' => 1000,
                    'product_data' => ['name' => 'Donation'],
                ],
            ]],
            'success_url' => self::SUCCESS_URL . '?session_id={CHECKOUT_SESSION_ID}',
            'cancel_url' => self::CANCEL_URL,
            'metadata' => ['source' => 'lodb-donate', 'kind' => 'donation'],
        ], $params);
    }

    public function testForDonorAttachesTheClientReferenceWithoutTouchingTheRest(): void
    {
        $base = CheckoutSessionParams::build(1000, 'Donation', self::SUCCESS_URL, self::CANCEL_URL);
        $params = CheckoutSessionParams::forDonor($base, 42);

        self::assertSame('42', $params['client_reference_id']);
        unset($params['client_reference_id']);
        self::assertSame($base, $params);
    }

    public function testAnonymousBuildCarriesNoClientReferenceButAlwaysTheDonationKind(): void
    {
        $params = CheckoutSessionParams::build(300, 'Donation', self::SUCCESS_URL, self::CANCEL_URL);

        self::assertArrayNotHasKey('client_reference_id', $params);
        self::assertSame(CheckoutSessionParams::KIND_DONATION, $params['metadata']['kind']);
    }

    public function testSessionIdPlaceholderIsAppendedVerbatimNotUrlencoded(): void
    {
        $params = CheckoutSessionParams::build(300, 'Donation', self::SUCCESS_URL, self::CANCEL_URL);

        // Stripe substitutes the placeholder server-side: %7B...%7D would break it.
        self::assertStringEndsWith('?session_id={CHECKOUT_SESSION_ID}', $params['success_url']);
        self::assertStringNotContainsString('%7B', $params['success_url']);
        self::assertStringNotContainsString('%7D', $params['success_url']);
    }

    public function testCurrencyAndAmountComeFromTheDonationPolicy(): void
    {
        $params = CheckoutSessionParams::build(2500, 'Gift', self::SUCCESS_URL, self::CANCEL_URL);
        $priceData = $params['line_items'][0]['price_data'];

        self::assertSame(DonationTiers::CURRENCY, $priceData['currency']);
        self::assertSame(2500, $priceData['unit_amount']);
        self::assertSame('Gift', $priceData['product_data']['name']);
    }
}
