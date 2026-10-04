<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

/**
 * Everything an API Checkout Session needs beyond the purchased item: who is
 * buying, and the absolute portal URLs Stripe returns them to. Grouped because
 * the three travel together through every payload builder.
 */
final readonly class ApiCheckoutContext
{
    public function __construct(
        public int $buyerId,
        public string $successUrl,
        public string $cancelUrl,
    ) {}
}
