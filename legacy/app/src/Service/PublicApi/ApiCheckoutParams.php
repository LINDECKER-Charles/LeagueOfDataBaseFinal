<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

use App\Entity\Enum\ApiPlan;

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
    public static function pack(
        ApiCreditPack $pack,
        string $productLabel,
        ApiCheckoutContext $context
    ): array {
        $buyerId = (string) $context->buyerId;

        return [
            'mode' => 'payment',
            'line_items' => [self::lineItem($pack->priceCents(), $productLabel)],
            'success_url' => $context->successUrl,
            'cancel_url' => $context->cancelUrl,
            'client_reference_id' => $buyerId,
            'metadata' => [
                'kind' => self::KIND_PACK,
                'user_id' => $buyerId,
                'requests' => (string) $pack->requests(),
            ],
        ];
    }

    /**
     * @return array<string, mixed> Checkout Session create params
     *
     * @throws \InvalidArgumentException when the plan has no subscription price
     */
    public static function plan(
        ApiPlan $plan,
        string $productLabel,
        ApiCheckoutContext $context
    ): array {
        $buyerId = (string) $context->buyerId;

        return [
            'mode' => 'subscription',
            'line_items' => [self::subscriptionItem($plan, $productLabel)],
            'success_url' => $context->successUrl,
            'cancel_url' => $context->cancelUrl,
            'client_reference_id' => $buyerId,
            'metadata' => [
                'kind' => self::KIND_PLAN,
                'user_id' => $buyerId,
                'plan' => $plan->value,
            ],
        ];
    }

    /**
     * Recurring line item of a plan — the only place the plan's purchasability
     * is asserted, since a plan without price or interval cannot be billed.
     *
     * @return array<string, mixed>
     *
     * @throws \InvalidArgumentException when the plan has no subscription price
     */
    private static function subscriptionItem(ApiPlan $plan, string $productLabel): array
    {
        $priceCents = $plan->priceCents();
        $interval = $plan->stripeInterval();
        if ($priceCents === null || $interval === null) {
            throw new \InvalidArgumentException(sprintf(
                'Plan "%s" is not purchasable as a subscription.',
                $plan->value
            ));
        }

        $item = self::lineItem($priceCents, $productLabel);
        $item['price_data']['recurring'] = ['interval' => $interval];

        return $item;
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
