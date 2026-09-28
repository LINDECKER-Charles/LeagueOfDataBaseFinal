<?php
declare(strict_types=1);

namespace App\Service\Donation;

/**
 * A donation Stripe reports as completed, as read off the Checkout Session:
 * the session id it is idempotent on, the amount charged, and the donor's
 * account when the flow carried one (null = anonymous gift).
 */
final readonly class CompletedDonation
{
    public function __construct(
        public string $sessionId,
        public int $amountCents,
        public string $currency,
        public ?int $userId = null,
    ) {}
}
