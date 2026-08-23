<?php
declare(strict_types=1);

namespace App\Tests\Unit\Controller\Billing;

use App\Controller\Billing\StripeWebhookController;
use App\Service\Stripe\StripeEventHandlerInterface;
use App\Tests\Unit\Support\RecordingLogger;
use PHPUnit\Framework\TestCase;
use Psr\Log\LoggerInterface;
use Psr\Log\LogLevel;
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

    /**
     * An empty secret answers 503 to EVERY call: Stripe eventually disables the
     * endpoint and purchased credits are never applied. Nothing self-heals — the
     * one situation the level table reserves for `critical`.
     */
    public function testAnUnconfiguredSecretIsCritical(): void
    {
        $logger = new RecordingLogger();

        $this->controller('', [], $logger)->handle(self::request('{}', null));

        self::assertSame(LogLevel::CRITICAL, $logger->only('stripe.webhook.unconfigured')['level']);
    }

    /** Either a secret rotated on one side only, or forged payloads. Both need a look. */
    public function testAnInvalidSignatureIsReported(): void
    {
        $logger = new RecordingLogger();

        $this->controller(self::SECRET, [], $logger)->handle(self::request('{}', 't=1,v1=dead'));

        self::assertSame(
            LogLevel::ERROR,
            $logger->only('stripe.webhook.signature_invalid')['level'],
        );
    }

    /**
     * No context key may be called `error`: the collector guesses `level` by regex
     * on the raw line, so the word alone would reclassify a record. `exception`
     * carries the object — class, file:line and cause — not just its message.
     */
    public function testAHandlerFailureCarriesTheExceptionObjectAndNoErrorKey(): void
    {
        $logger = new RecordingLogger();
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

        $this->controller(self::SECRET, [$handler], $logger)
            ->handle(self::request($payload, self::sign($payload)));

        $context = $logger->only('stripe.webhook.handler_failed')['context'];

        self::assertArrayNotHasKey('error', $context);
        self::assertInstanceOf(\RuntimeException::class, $context['exception']);
    }

    /** A webhook that worked must stay silent. */
    public function testASuccessfulDispatchLogsNothing(): void
    {
        $logger = new RecordingLogger();
        $payload = json_encode([
            'id' => 'evt_ok',
            'object' => 'event',
            'type' => 'checkout.session.completed',
            'data' => ['object' => ['id' => 'cs_1', 'object' => 'checkout.session']],
        ], JSON_THROW_ON_ERROR);

        $this->controller(self::SECRET, [], $logger)
            ->handle(self::request($payload, self::sign($payload)));

        self::assertSame([], $logger->keys());
    }

    /** @param list<StripeEventHandlerInterface> $handlers */
    private function controller(
        string $secret,
        array $handlers,
        ?LoggerInterface $logger = null,
    ): StripeWebhookController {
        return new StripeWebhookController($secret, $handlers, $logger ?? new NullLogger());
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

        return sprintf(
            't=%d,v1=%s',
            $timestamp,
            hash_hmac('sha256', $timestamp . '.' . $payload, self::SECRET),
        );
    }
}
