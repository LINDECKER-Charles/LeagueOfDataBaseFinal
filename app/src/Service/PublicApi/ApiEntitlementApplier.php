<?php
declare(strict_types=1);

namespace App\Service\PublicApi;

use App\Entity\ApiKey;
use App\Entity\ApiPlan;
use App\Repository\ApiKeyRepository;
use App\Repository\UserRepository;
use Doctrine\ORM\EntityManagerInterface;
use Psr\Log\LoggerInterface;

/**
 * Applies paid entitlements (credit packs, plan subscriptions, subscription
 * ends) to the owner's active key. Called from the Stripe webhook: malformed
 * business data is logged and swallowed (a retry cannot fix it), while
 * infrastructure failures bubble up so Stripe redelivers the event.
 * Logs carry internal ids only — never customer identity.
 */
final class ApiEntitlementApplier implements ApiEntitlements
{
    public function __construct(
        private readonly ApiKeyRepository $apiKeys,
        private readonly UserRepository $users,
        private readonly ApiKeyIssuer $issuer,
        private readonly EntityManagerInterface $entityManager,
        private readonly LoggerInterface $logger,
    ) {}

    public function applyPack(int $userId, int $requests): void
    {
        if ($requests <= 0) {
            $this->logger->warning('stripe.api.pack_without_requests', ['user_id' => $userId]);

            return;
        }

        $key = $this->activeKeyFor($userId);
        if ($key === null) {
            return;
        }

        $this->apiKeys->addCredits($key, $requests, ApiPlan::RATE_CREDITS);
        $this->logger->info('stripe.api.credits_added', ['api_key_id' => $key->getId(), 'requests' => $requests]);
    }

    public function applyPlan(int $userId, ApiPlan $plan, ?string $customerId, ?string $subscriptionId): void
    {
        if (!$plan->isSubscription()) {
            $this->logger->warning('stripe.api.plan_not_subscribable', ['plan' => $plan->value]);

            return;
        }

        $key = $this->activeKeyFor($userId);
        if ($key === null) {
            return;
        }

        $key->applyPlan($plan);
        $key->attachStripe($customerId, $subscriptionId);
        $this->entityManager->flush();
        $this->logger->info('stripe.api.plan_activated', ['api_key_id' => $key->getId(), 'plan' => $plan->value]);
    }

    /** Subscription cancelled/expired on Stripe's side: back to free, credits kept. */
    public function releaseSubscription(string $subscriptionId): void
    {
        $key = $this->apiKeys->findOneActiveByStripeSubscription($subscriptionId);
        if ($key === null) {
            // Redelivered event, or key revoked meanwhile: nothing left to downgrade.
            $this->logger->info('stripe.api.subscription_unmatched');

            return;
        }

        $key->downgradeToFree();
        $this->entityManager->flush();
        $this->logger->info('stripe.api.plan_released', ['api_key_id' => $key->getId()]);
    }

    /**
     * Active key of the user, provisioning a free one when absent (paid while
     * keyless — e.g. revoked between checkout and webhook). The auto-created
     * secret is discarded on purpose: the owner regenerates from the portal,
     * which carries the entitlements onto a key whose secret they can see.
     */
    private function activeKeyFor(int $userId): ?ApiKey
    {
        $user = $this->users->find($userId);
        if ($user === null) {
            $this->logger->warning('stripe.api.unknown_user', ['user_id' => $userId]);

            return null;
        }

        $key = $this->apiKeys->findActiveByUser($user);
        if ($key !== null) {
            return $key;
        }

        $issued = $this->issuer->issue($user);
        $this->entityManager->persist($issued->key);
        // Flush now: addCredits() needs the id for its SQL-level increment.
        $this->entityManager->flush();
        $this->logger->info('stripe.api.key_autoprovisioned', ['api_key_id' => $issued->key->getId()]);

        return $issued->key;
    }
}
