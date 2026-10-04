<?php
declare(strict_types=1);

namespace App\Service\Stripe;

use App\Service\PublicApi\ApiEntitlements;
use Stripe\Event;
use Stripe\StripeObject;

/**
 * customer.subscription.deleted — the paid API plan ends (cancellation or
 * final payment failure): the matching key falls back to the free plan,
 * prepaid credits untouched.
 */
final readonly class SubscriptionDeletedHandler implements StripeEventHandlerInterface
{
    private const EVENT_TYPE = 'customer.subscription.deleted';

    public function __construct(
        private ApiEntitlements $entitlements,
    ) {}

    public function eventType(): string
    {
        return self::EVENT_TYPE;
    }

    public function handle(Event $event): void
    {
        /** @var StripeObject $subscription */
        $subscription = $event->data->object;

        $this->entitlements->releaseSubscription((string) $subscription->id);
    }
}
