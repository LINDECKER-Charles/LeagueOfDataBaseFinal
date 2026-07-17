<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Stripe;

use App\Entity\ApiPlan;
use App\Service\Donation\DonationLedger;
use App\Service\PublicApi\ApiEntitlements;
use App\Service\Stripe\CheckoutSessionCompletedHandler;
use App\Service\Stripe\SubscriptionDeletedHandler;
use PHPUnit\Framework\TestCase;
use Psr\Log\NullLogger;
use Stripe\Event;

/**
 * Routing of verified Stripe events onto the entitlement and donation ports,
 * from fixture payloads only — no Stripe API call anywhere.
 */
final class StripeEventHandlersTest extends TestCase
{
    public function testApiPackCheckoutCreditsTheBuyer(): void
    {
        $entitlements = $this->createMock(ApiEntitlements::class);
        $entitlements->expects(self::once())->method('applyPack')->with(42, 5_000);
        $entitlements->expects(self::never())->method('applyPlan');

        $event = self::checkoutEvent([
            'metadata' => ['kind' => 'api_pack', 'user_id' => '42', 'requests' => '5000'],
        ]);

        $this->handler($entitlements, $this->neverCalledLedger())->handle($event);
    }

    public function testApiPlanCheckoutActivatesTheSubscription(): void
    {
        $entitlements = $this->createMock(ApiEntitlements::class);
        $entitlements->expects(self::once())
            ->method('applyPlan')
            ->with(42, ApiPlan::Monthly, 'cus_42', 'sub_42');

        $event = self::checkoutEvent([
            'customer' => 'cus_42',
            'subscription' => 'sub_42',
            'metadata' => ['kind' => 'api_plan', 'user_id' => '42', 'plan' => 'monthly'],
        ]);

        $this->handler($entitlements, $this->neverCalledLedger())->handle($event);
    }

    public function testUnknownPlanValueIsSwallowedWithoutTouchingEntitlements(): void
    {
        $entitlements = $this->createMock(ApiEntitlements::class);
        $entitlements->expects(self::never())->method('applyPlan');

        $event = self::checkoutEvent([
            'metadata' => ['kind' => 'api_plan', 'user_id' => '42', 'plan' => 'gold_tier'],
        ]);

        $this->handler($entitlements, $this->neverCalledLedger())->handle($event);
    }

    public function testDonationCheckoutIsRecordedWithTheSignedInDonor(): void
    {
        $ledger = $this->createMock(DonationLedger::class);
        $ledger->expects(self::once())->method('record')->with('cs_don', 500, 'eur', 7);

        $event = self::checkoutEvent([
            'id' => 'cs_don',
            'amount_total' => 500,
            'currency' => 'eur',
            'client_reference_id' => '7',
            'metadata' => ['source' => 'lodb-donate', 'kind' => 'donation'],
        ]);

        $this->handler($this->untouchedEntitlements(), $ledger)->handle($event);
    }

    public function testLegacyDonationWithoutKindStillLandsInTheDonationBranch(): void
    {
        // Sessions created before the `kind` discriminator existed must keep
        // routing to the donation branch — anonymously (no reference).
        $ledger = $this->createMock(DonationLedger::class);
        $ledger->expects(self::once())->method('record')->with('cs_legacy', 1000, 'eur', null);

        $event = self::checkoutEvent([
            'id' => 'cs_legacy',
            'amount_total' => 1000,
            'currency' => 'eur',
            'metadata' => ['source' => 'lodb-donate'],
        ]);

        $this->handler($this->untouchedEntitlements(), $ledger)->handle($event);
    }

    public function testNonNumericClientReferenceIsTreatedAsAnonymous(): void
    {
        $ledger = $this->createMock(DonationLedger::class);
        $ledger->expects(self::once())->method('record')->with('cs_1', 300, 'eur', null);

        $event = self::checkoutEvent([
            'amount_total' => 300,
            'currency' => 'eur',
            'client_reference_id' => 'not-a-user-id',
            'metadata' => ['kind' => 'donation'],
        ]);

        $this->handler($this->untouchedEntitlements(), $ledger)->handle($event);
    }

    public function testSubscriptionDeletedReleasesTheMatchingKey(): void
    {
        $entitlements = $this->createMock(ApiEntitlements::class);
        $entitlements->expects(self::once())->method('releaseSubscription')->with('sub_42');

        $event = Event::constructFrom([
            'id' => 'evt_2',
            'type' => 'customer.subscription.deleted',
            'data' => ['object' => ['id' => 'sub_42', 'object' => 'subscription']],
        ]);

        new SubscriptionDeletedHandler($entitlements)->handle($event);
    }

    public function testHandlersDeclareTheEventTypesTheWebhookRoutesOn(): void
    {
        $entitlements = $this->createStub(ApiEntitlements::class);

        self::assertSame(
            'checkout.session.completed',
            $this->handler($entitlements, $this->createStub(DonationLedger::class))->eventType(),
        );
        self::assertSame(
            'customer.subscription.deleted',
            new SubscriptionDeletedHandler($entitlements)->eventType(),
        );
    }

    private function handler(ApiEntitlements $entitlements, DonationLedger $ledger): CheckoutSessionCompletedHandler
    {
        return new CheckoutSessionCompletedHandler($entitlements, $ledger, new NullLogger());
    }

    private function untouchedEntitlements(): ApiEntitlements
    {
        $entitlements = $this->createMock(ApiEntitlements::class);
        $entitlements->expects(self::never())->method('applyPack');
        $entitlements->expects(self::never())->method('applyPlan');

        return $entitlements;
    }

    private function neverCalledLedger(): DonationLedger
    {
        $ledger = $this->createMock(DonationLedger::class);
        $ledger->expects(self::never())->method('record');

        return $ledger;
    }

    /** @param array<string, mixed> $session */
    private static function checkoutEvent(array $session): Event
    {
        return Event::constructFrom([
            'id' => 'evt_1',
            'type' => 'checkout.session.completed',
            'data' => ['object' => $session + ['id' => 'cs_1', 'object' => 'checkout.session']],
        ]);
    }
}
