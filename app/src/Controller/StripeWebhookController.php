<?php
declare(strict_types=1);

namespace App\Controller;

use Psr\Log\LoggerInterface;
use Stripe\Checkout\Session;
use Stripe\Event;
use Stripe\Exception\SignatureVerificationException;
use Stripe\Webhook;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Stripe webhook endpoint — signature-verified, JSON only (no HTML layout, so
 * plain AbstractController). Donations are not persisted: completed checkouts
 * are only logged, the Stripe dashboard remains the source of truth.
 */
final class StripeWebhookController extends AbstractController
{
    private const EVENT_CHECKOUT_COMPLETED = 'checkout.session.completed';

    public function __construct(
        #[Autowire(env: 'STRIPE_WEBHOOK_SECRET')] #[\SensitiveParameter] private readonly string $webhookSecret,
        private readonly LoggerInterface $logger,
    ) {}

    #[Route('/webhooks/stripe', name: 'app_stripe_webhook', methods: ['POST'])]
    public function handle(Request $request): JsonResponse
    {
        if ($this->webhookSecret === '') {
            return new JsonResponse(['error' => 'webhook not configured'], Response::HTTP_SERVICE_UNAVAILABLE);
        }

        try {
            $event = Webhook::constructEvent(
                $request->getContent(),
                (string) $request->headers->get('Stripe-Signature', ''),
                $this->webhookSecret,
            );
        } catch (SignatureVerificationException | \UnexpectedValueException) {
            return new JsonResponse(['error' => 'invalid payload or signature'], Response::HTTP_BAD_REQUEST);
        }

        if ($event->type === self::EVENT_CHECKOUT_COMPLETED) {
            $this->logCompletedCheckout($event);
        }

        return new JsonResponse(['received' => true]);
    }

    private function logCompletedCheckout(Event $event): void
    {
        /** @var Session $session */
        $session = $event->data->object;

        // Amount and currency only — never the donor's email or name.
        $this->logger->info('stripe.donation.completed', [
            'session' => $session->id,
            'amount_total' => $session->amount_total,
            'currency' => $session->currency,
        ]);
    }
}
