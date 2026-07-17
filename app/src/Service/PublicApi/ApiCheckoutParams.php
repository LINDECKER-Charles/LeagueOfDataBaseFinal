<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

use App\Entity\ApiPlan;

/**
 * Pure builder for the Stripe Checkout Session payloads of the API billing:
 * one-time credit packs (mode payment) and plan subscriptions (mode
 * subscription), both with inline price_data — no Stripe product catalog to
 * maintain. The metadata `kind` is the webhook dispatch contract.
 */
final class ApiCheckoutParams
{
    public const KIND_PACK = 'api_pack';
    public const KIND_PLAN = 'api_plan';
    public const CURRENCY = 'eur';

    private function __construct()
    {
        // Static builder — never instantiated.
    }

    /** @return array<string, mixed> Checkout Session create params */
    public static function pack(int $userId, ApiCreditPack $pack, string $productLabel, CheckoutReturnUrls $urls): array
    {
        return [
            'mode' => 'payment',
            'line_items' => [self::lineItem($pack->priceCents(), $productLabel)],
            'success_url' => $urls->success,
            'cancel_url' => $urls->cancel,
            'client_reference_id' => (string) $userId,
            'metadata' => [
                'kind' => self::KIND_PACK,
                'user_id' => (string) $userId,
                'requests' => (string) $pack->requests(),
            ],
        ];
    }

    /**
     * @return array<string, mixed> Checkout Session create params
     *
     * @throws \InvalidArgumentException when the plan has no subscription price
     */
    public static function plan(int $userId, ApiPlan $plan, string $productLabel, CheckoutReturnUrls $urls): array
    {
        $priceCents = $plan->priceCents();
        $interval = $plan->stripeInterval();
        if ($priceCents === null || $interval === null) {
            throw new \InvalidArgumentException(sprintf('Plan "%s" is not purchasable as a subscription.', $plan->value));
        }

        $item = self::lineItem($priceCents, $productLabel);
        $item['price_data']['recurring'] = ['interval' => $interval];

        return [
            'mode' => 'subscription',
            'line_items' => [$item],
            'success_url' => $urls->success,
            'cancel_url' => $urls->cancel,
            'client_reference_id' => (string) $userId,
            'metadata' => [
                'kind' => self::KIND_PLAN,
                'user_id' => (string) $userId,
                'plan' => $plan->value,
            ],
        ];
    }

    /** @return array<string, mixed> */
    private static function lineItem(int $unitAmountCents, string $productLabel): array
    {
        return [
            'quantity' => 1,
            'price_data' => [
                'currency' => self::CURRENCY,
                'unit_amount' => $unitAmountCents,
                'product_data' => ['name' => $productLabel],
            ],
        ];
    }
}
