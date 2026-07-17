<?php
declare(strict_types=1);

namespace App\Service\Donation;

use Stripe\StripeClient;
use Symfony\Component\DependencyInjection\Attribute\Autowire;

/**
 * Thin gateway to Stripe Checkout. Holds the only reference to the secret key;
 * callers deal in validated params (CheckoutSessionParams) and redirect URLs.
 * Stripe API failures (\Stripe\Exception\ApiErrorException) bubble up — the
 * controller owns the user-facing fallback.
 */
final readonly class StripeCheckout
{
    public function __construct(
        #[Autowire(env: 'STRIPE_SECRET_KEY')] #[\SensitiveParameter] private string $secretKey,
    ) {}

    /** Empty key = donations deliberately disabled; the UI degrades instead of erroring. */
    public function isConfigured(): bool
    {
        return $this->secretKey !== '';
    }

    /**
     * Creates a Checkout Session and returns its hosted-page URL.
     *
     * @param array<string, mixed> $params See CheckoutSessionParams::build()
     *
     * @throws \Stripe\Exception\ApiErrorException
     */
    public function createSession(array $params): string
    {
        $session = (new StripeClient($this->secretKey))->checkout->sessions->create($params);

        if (!\is_string($session->url) || $session->url === '') {
            throw new \RuntimeException('Stripe returned a Checkout Session without a redirect URL.');
        }

        return $session->url;
    }
}
