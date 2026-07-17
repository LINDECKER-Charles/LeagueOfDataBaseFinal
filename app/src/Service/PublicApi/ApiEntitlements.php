<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

use App\Entity\ApiPlan;

/**
 * Port between the Stripe webhook layer and the API key persistence: what a
 * completed purchase (or an ended subscription) does to the owner's key.
 * Implementations must treat malformed business data as log-and-swallow and
 * let infrastructure failures bubble (Stripe redelivers on non-2xx).
 */
interface ApiEntitlements
{
    /** Credits the one-time pack onto the buyer's active key (creating a free key if none). */
    public function applyPack(int $userId, int $requests): void;

    /** Activates a subscription plan on the buyer's active key (creating a free key if none). */
    public function applyPlan(int $userId, ApiPlan $plan, ?string $customerId, ?string $subscriptionId): void;

    /** Subscription cancelled/expired on Stripe's side: back to free, credits kept. */
    public function releaseSubscription(string $subscriptionId): void;
}
