<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Donation;

use App\Controller\StripeWebhookController;
use App\Entity\Build;
use App\Entity\Donation;
use App\Entity\User;
use App\Repository\DonationRepository;
use App\Repository\UserRepository;
use App\Service\Donation\DonationRecorder;
use App\Service\PublicApi\ApiEntitlements;
use App\Service\Stripe\CheckoutSessionCompletedHandler;
use App\Tests\Unit\Support\InMemoryOrm;
use Doctrine\ORM\EntityManager;
use Doctrine\Persistence\ManagerRegistry;
use PHPUnit\Framework\TestCase;
use Psr\Log\NullLogger;
use Symfony\Component\HttpFoundation\Request;

/**
 * The full donation persistence chain on REAL storage (in-memory SQLite) and a
 * REAL HMAC-signed webhook payload: signature → dispatch → ledger row +
 * supporter flag, idempotent on Stripe redelivery.
 */
final class DonationRecorderTest extends TestCase
{
    private const SECRET = 'whsec_test_secret';

    private EntityManager $entityManager;
    private DonationRepository $donations;
    private StripeWebhookController $controller;

    protected function setUp(): void
    {
        $this->entityManager = InMemoryOrm::entityManager([User::class, Build::class, Donation::class]);

        $registry = $this->createStub(ManagerRegistry::class);
        $registry->method('getManagerForClass')->willReturn($this->entityManager);
        $this->donations = new DonationRepository($registry);

        $recorder = new DonationRecorder(
            $this->donations,
            new UserRepository($registry),
            $this->entityManager,
            new NullLogger(),
        );
        $handler = new CheckoutSessionCompletedHandler(
            $this->createStub(ApiEntitlements::class),
            $recorder,
            new NullLogger(),
        );
        $this->controller = new StripeWebhookController(self::SECRET, [$handler], new NullLogger());
    }

    public function testSignedDonationOfASignedInDonorIsPersistedAndGrantsTheBadge(): void
    {
        $user = $this->givenUser('donor-a');

        $response = $this->controller->handle(self::signedRequest(self::donationPayload(
            'cs_linked',
            500,
            (string) $user->getId(),
        )));

        self::assertSame(200, $response->getStatusCode());
        self::assertTrue($this->donations->existsBySessionId('cs_linked'));
        self::assertTrue($user->isSupporter());
        self::assertSame(500, $this->donations->sumForUser($user));
    }

    public function testAnonymousSignedDonationIsPersistedWithoutAnyAccountLink(): void
    {
        $response = $this->controller->handle(self::signedRequest(self::donationPayload('cs_anon', 1000, null)));

        self::assertSame(200, $response->getStatusCode());
        $donation = $this->donations->findOneBy(['stripeSessionId' => 'cs_anon']);
        self::assertNotNull($donation);
        self::assertNull($donation->getUser());
        self::assertSame(1000, $donation->getAmountCents());
        self::assertSame('eur', $donation->getCurrency());
    }

    public function testRedeliveredEventIsASilentNoOp(): void
    {
        $user = $this->givenUser('donor-b');
        $payload = self::donationPayload('cs_replayed', 2500, (string) $user->getId());

        $first = $this->controller->handle(self::signedRequest($payload));
        $second = $this->controller->handle(self::signedRequest($payload));

        self::assertSame(200, $first->getStatusCode());
        self::assertSame(200, $second->getStatusCode());
        self::assertSame(1, $this->donations->countAll());
        self::assertSame(2500, $this->donations->sumAll());
    }

    public function testUnknownDonorReferenceStillRecordsTheDonationUnlinked(): void
    {
        $response = $this->controller->handle(self::signedRequest(self::donationPayload('cs_ghost', 300, '9999')));

        self::assertSame(200, $response->getStatusCode());
        self::assertNull($this->donations->findOneBy(['stripeSessionId' => 'cs_ghost'])?->getUser());
    }

    private function givenUser(string $username): User
    {
        $user = new User()->setEmail($username . '@example.test')->setUsername($username);
        $this->entityManager->persist($user);
        $this->entityManager->flush();

        return $user;
    }

    private static function donationPayload(string $sessionId, int $amountCents, ?string $reference): string
    {
        $session = [
            'id' => $sessionId,
            'object' => 'checkout.session',
            'amount_total' => $amountCents,
            'currency' => 'eur',
            'client_reference_id' => $reference,
            'metadata' => ['source' => 'lodb-donate', 'kind' => 'donation'],
        ];

        return json_encode([
            'id' => 'evt_' . $sessionId,
            'object' => 'event',
            'type' => 'checkout.session.completed',
            'data' => ['object' => $session],
        ], JSON_THROW_ON_ERROR);
    }

    private static function signedRequest(string $payload): Request
    {
        $request = Request::create('/webhooks/stripe', 'POST', content: $payload);
        $timestamp = time();
        $request->headers->set('Stripe-Signature', sprintf(
            't=%d,v1=%s',
            $timestamp,
            hash_hmac('sha256', $timestamp . '.' . $payload, self::SECRET),
        ));

        return $request;
    }
}
