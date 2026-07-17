<?php
declare(strict_types=1);

namespace App\Service\Donation;

/**
 * Port between the Stripe webhook layer and donation persistence: what a
 * completed donation Checkout does to the ledger (and to the donor's account).
 * Implementations must be idempotent on the session id — Stripe redelivers —
 * and let infrastructure failures bubble so the webhook answers non-2xx.
 */
interface DonationLedger
{
    /**
     * Records a completed donation once; a replayed session id is a silent
     * no-op. When $userId resolves to an existing account, the donation is
     * linked to it and the account becomes a supporter.
     */
    public function record(string $sessionId, int $amountCents, string $currency, ?int $userId): void;
}
