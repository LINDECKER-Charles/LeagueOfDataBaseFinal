<?php
declare(strict_types=1);

namespace App\Tests\Unit\Controller;

use App\Controller\StripeWebhookController;
use App\Service\Stripe\StripeEventHandlerInterface;
use PHPUnit\Framework\TestCase;
use Psr\Log\NullLogger;
use Stripe\Event;
use Symfony\Component\HttpFoundation\Request;

/**
 * The webhook endpoint contract, without any Stripe call: 503 unconfigured,
 * 400 on missing/invalid signature (preserved behavior), dispatch to the
 * matching tagged handler on a correctly signed payload, 500 on handler crash.
 */
final class StripeWebhookControllerTest extends TestCase
{
    private const SECRET = 'whsec_test_secret';

    public function testUnconfiguredSecretAnswers503(): void
    {
        $response = $this->controller('', [])->handle(self::request('{}', null));

        self::assertSame(503, $response->getStatusCode());
    }

    public function testMissingSignatureAnswers400(): void
    {
        $response = $this->controller(self::SECRET, [])->handle(self::request('{}', null));

        self::assertSame(400, $response->getStatusCode());
    }

    public function testTamperedSignatureAnswers400(): void
    {
        $response = $this->controller(self::SECRET, [])
            ->handle(self::request('{}', 't=123,v1=deadbeef'));

        self::assertSame(400, $response->getStatusCode());
    }

    public function testSignedEventIsDispatchedToTheMatchingHandler(): void
    {
        $handler = new class implements StripeEventHandlerInterface {
            public ?string $handledEventId = null;

            public function eventType(): string
            {
                return 'checkout.session.completed';
            }

            public function handle(Event $event): void
            {
                $this->handledEventId = $event->id;
            }
        };

        $payload = json_encode([
            'id' => 'evt_signed',
            'object' => 'event',
            'type' => 'checkout.session.completed',
            'data' => ['object' => ['id' => 'cs_1', 'object' => 'checkout.session']],
        ], JSON_THROW_ON_ERROR);

        $response = $this->controller(self::SECRET, [$handler])
            ->handle(self::request($payload, self::sign($payload)));

        self::assertSame(200, $response->getStatusCode());
        self::assertSame('evt_signed', $handler->handledEventId);
        self::assertSame(['received' => true], json_decode((string) $response->getContent(), true));
    }

    public function testHandlerFailureAnswers500SoStripeRedelivers(): void
    {
        $handler = new class implements StripeEventHandlerInterface {
            public function eventType(): string
            {
                return 'checkout.session.completed';
            }

            public function handle(Event $event): void
            {
                throw new \RuntimeException('database gone');
            }
        };

        $payload = json_encode([
            'id' => 'evt_boom',
            'object' => 'event',
            'type' => 'checkout.session.completed',
            'data' => ['object' => ['id' => 'cs_1', 'object' => 'checkout.session']],
        ], JSON_THROW_ON_ERROR);

        $response = $this->controller(self::SECRET, [$handler])
            ->handle(self::request($payload, self::sign($payload)));

        self::assertSame(500, $response->getStatusCode());
    }

    /** @param list<StripeEventHandlerInterface> $handlers */
    private function controller(string $secret, array $handlers): StripeWebhookController
    {
        return new StripeWebhookController($secret, $handlers, new NullLogger());
    }

    private static function request(string $payload, ?string $signature): Request
    {
        $request = Request::create('/webhooks/stripe', 'POST', content: $payload);
        if ($signature !== null) {
            $request->headers->set('Stripe-Signature', $signature);
        }

        return $request;
    }

    /** Stripe's v1 scheme: HMAC-SHA256 of "<timestamp>.<payload>" with the endpoint secret. */
    private static function sign(string $payload): string
    {
        $timestamp = time();

        return sprintf('t=%d,v1=%s', $timestamp, hash_hmac('sha256', $timestamp . '.' . $payload, self::SECRET));
    }
}
