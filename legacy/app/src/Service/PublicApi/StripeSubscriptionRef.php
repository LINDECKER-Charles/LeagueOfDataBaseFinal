<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

/**
 * The Stripe customer/subscription pair a plan activation attaches to a key, so
 * a later `customer.subscription.deleted` can find it back. Either side may be
 * absent — a session Stripe reports without them still activates the plan.
 */
final readonly class StripeSubscriptionRef
{
    public function __construct(
        public ?string $customerId = null,
        public ?string $subscriptionId = null,
    ) {}
}
