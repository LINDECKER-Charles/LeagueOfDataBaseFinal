<?php
declare(strict_types=1);

namespace App\Service\Stripe;

use App\Entity\ApiPlan;
use App\Service\Donation\DonationLedger;
use App\Service\PublicApi\ApiCheckoutParams;
use App\Service\PublicApi\ApiEntitlements;
use Psr\Log\LoggerInterface;
use Stripe\Event;
use Stripe\StripeObject;

/**
 * checkout.session.completed — dispatched on the session's metadata `kind`:
 * API credit packs and plan subscriptions mutate the buyer's key; the default
 * branch (kind `donation` AND historical sessions without any kind) records
 * the donation in the local ledger — idempotent on the session id, linking
 * the donor's account when present. Never logs customer identity — session
 * id, amounts and internal ids only.
 */
final readonly class CheckoutSessionCompletedHandler implements StripeEventHandlerInterface
{
    private const EVENT_TYPE = 'checkout.session.completed';

    public function __construct(
        private ApiEntitlements $entitlements,
        private DonationLedger $donations,
        private LoggerInterface $logger,
    ) {}

    public function eventType(): string
    {
        return self::EVENT_TYPE;
    }

    public function handle(Event $event): void
    {
        /** @var StripeObject $session */
        $session = $event->data->object;
        $metadata = $session->metadata instanceof StripeObject ? $session->metadata->toArray() : [];

        match ($metadata['kind'] ?? null) {
            ApiCheckoutParams::KIND_PACK => $this->entitlements->applyPack(
                $this->userId($session, $metadata),
                (int) ($metadata['requests'] ?? 0),
            ),
            ApiCheckoutParams::KIND_PLAN => $this->applyPlan($session, $metadata),
            default => $this->recordDonation($session),
        };
    }

    /** @param array<string, mixed> $metadata */
    private function applyPlan(StripeObject $session, array $metadata): void
    {
        $plan = ApiPlan::tryFrom((string) ($metadata['plan'] ?? ''));
        if ($plan === null) {
            $this->logger->warning('stripe.api.unknown_plan', ['session' => $session->id]);

            return;
        }

        $this->entitlements->applyPlan(
            $this->userId($session, $metadata),
            $plan,
            \is_string($session->customer) ? $session->customer : null,
            \is_string($session->subscription) ? $session->subscription : null,
        );
    }

    /** @param array<string, mixed> $metadata */
    private function userId(StripeObject $session, array $metadata): int
    {
        return (int) ($metadata['user_id'] ?? $session->client_reference_id ?? 0);
    }

    /** Donations: persist through the ledger; the donor id travels as client_reference_id. */
    private function recordDonation(StripeObject $session): void
    {
        $reference = $session->client_reference_id ?? null;

        $this->donations->record(
            (string) $session->id,
            (int) ($session->amount_total ?? 0),
            (string) ($session->currency ?? ''),
            \is_string($reference) && ctype_digit($reference) ? (int) $reference : null,
        );
    }
}
