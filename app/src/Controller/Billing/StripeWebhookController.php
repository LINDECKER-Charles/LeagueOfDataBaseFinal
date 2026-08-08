<?php
declare(strict_types=1);

namespace App\Controller\Billing;

use App\Service\Stripe\StripeEventHandlerInterface;
use Psr\Log\LoggerInterface;
use Stripe\Event;
use Stripe\Exception\SignatureVerificationException;
use Stripe\Webhook;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\DependencyInjection\Attribute\Autowire;
use Symfony\Component\DependencyInjection\Attribute\AutowireIterator;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Attribute\Route;

/**
 * Stripe webhook endpoint — signature-verified, JSON only (no HTML layout, so
 * plain AbstractController). Business reactions live in tagged
 * StripeEventHandlerInterface services (API billing today, persisted donations
 * tomorrow); event types nobody handles are acknowledged silently.
 */
final class StripeWebhookController extends AbstractController
{
    /**
     * @param iterable<StripeEventHandlerInterface> $handlers
     */
    public function __construct(
        #[Autowire(env: 'STRIPE_WEBHOOK_SECRET')]
        #[\SensitiveParameter]
        private readonly string $webhookSecret,
        #[AutowireIterator(StripeEventHandlerInterface::TAG)] private readonly iterable $handlers,
        private readonly LoggerInterface $logger,
    ) {}

    #[Route('/webhooks/stripe', name: 'app_stripe_webhook', methods: ['POST'])]
    public function handle(Request $request): JsonResponse
    {
        if ($this->webhookSecret === '') {
            return new JsonResponse(
                ['error' => 'webhook not configured'],
                Response::HTTP_SERVICE_UNAVAILABLE
            );
        }

        try {
            $event = $this->verifiedEvent($request);
        } catch (SignatureVerificationException | \UnexpectedValueException) {
            return new JsonResponse(
                ['error' => 'invalid payload or signature'],
                Response::HTTP_BAD_REQUEST
            );
        }

        try {
            $this->dispatch($event);
        } catch (\Throwable $e) {
            return $this->handlerFailure($event, $e);
        }

        return new JsonResponse(['received' => true]);
    }

    private function verifiedEvent(Request $request): Event
    {
        return Webhook::constructEvent(
            $request->getContent(),
            (string) $request->headers->get('Stripe-Signature', ''),
            $this->webhookSecret,
        );
    }

    private function dispatch(Event $event): void
    {
        foreach ($this->handlers as $handler) {
            if ($handler->eventType() === $event->type) {
                $handler->handle($event);
            }
        }
    }

    /** Infrastructure failure: non-2xx so Stripe redelivers the event. */
    private function handlerFailure(Event $event, \Throwable $e): JsonResponse
    {
        $this->logger->error('stripe.webhook.handler_failed', [
            'event' => $event->id,
            'type' => $event->type,
            'error' => $e->getMessage(),
        ]);

        return new JsonResponse(
            ['error' => 'handler failure'],
            Response::HTTP_INTERNAL_SERVER_ERROR
        );
    }
}
