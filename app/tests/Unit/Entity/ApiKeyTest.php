<?php
declare(strict_types=1);

namespace App\Tests\Unit\Entity;

use App\Entity\ApiKey;
use App\Entity\ApiPlan;
use App\Entity\User;
use PHPUnit\Framework\TestCase;

final class ApiKeyTest extends TestCase
{
    public function testNewKeyStartsOnTheFreePlanDefaults(): void
    {
        $key = $this->makeKey();

        self::assertSame(ApiPlan::Free, $key->getPlan());
        self::assertSame(ApiPlan::QUOTA_FREE, $key->getMonthlyQuota());
        self::assertSame(ApiPlan::RATE_FREE, $key->getRateLimitPerMin());
        self::assertSame(0, $key->getCreditsBalance());
        self::assertTrue($key->isActive());
        self::assertNull($key->getRevokedAt());
    }

    public function testApplyPlanSetsQuotaAndRateFromThePolicy(): void
    {
        $key = $this->makeKey();

        $key->applyPlan(ApiPlan::Annual);

        self::assertSame(ApiPlan::Annual, $key->getPlan());
        self::assertSame(ApiPlan::QUOTA_ANNUAL, $key->getMonthlyQuota());
        self::assertSame(ApiPlan::RATE_ANNUAL, $key->getRateLimitPerMin());
    }

    public function testDowngradeToFreeKeepsCreditsAndTheirRateFloor(): void
    {
        $key = $this->makeKey();
        $key->applyPlan(ApiPlan::Monthly);
        $key->attachStripe('cus_1', 'sub_1');
        $this->setCredits($key, 5_000);

        $key->downgradeToFree();

        self::assertSame(ApiPlan::Free, $key->getPlan());
        self::assertSame(ApiPlan::QUOTA_FREE, $key->getMonthlyQuota());
        // Product rule: credits > 0 entitle at least 60 req/min even on free.
        self::assertSame(ApiPlan::RATE_CREDITS, $key->getRateLimitPerMin());
        self::assertSame(5_000, $key->getCreditsBalance());
        self::assertNull($key->getStripeSubscriptionId());
        self::assertSame('cus_1', $key->getStripeCustomerId());
    }

    public function testDowngradeToFreeWithoutCreditsFallsBackToTheFreeRate(): void
    {
        $key = $this->makeKey();
        $key->applyPlan(ApiPlan::MonthlyPlus);

        $key->downgradeToFree();

        self::assertSame(ApiPlan::RATE_FREE, $key->getRateLimitPerMin());
    }

    public function testRevokeDeactivatesAndTimestamps(): void
    {
        $key = $this->makeKey();

        $key->revoke();

        self::assertFalse($key->isActive());
        self::assertNotNull($key->getRevokedAt());
    }

    public function testCarryEntitlementsCopiesPlanQuotaCreditsRateAndStripeIds(): void
    {
        $previous = $this->makeKey();
        $previous->applyPlan(ApiPlan::AnnualPlus);
        $previous->attachStripe('cus_9', 'sub_9');
        $this->setCredits($previous, 1_234);

        $replacement = $this->makeKey();
        $replacement->carryEntitlementsFrom($previous);

        self::assertSame(ApiPlan::AnnualPlus, $replacement->getPlan());
        self::assertSame(ApiPlan::QUOTA_ANNUAL_PLUS, $replacement->getMonthlyQuota());
        self::assertSame(1_234, $replacement->getCreditsBalance());
        self::assertSame($previous->getRateLimitPerMin(), $replacement->getRateLimitPerMin());
        self::assertSame('cus_9', $replacement->getStripeCustomerId());
        self::assertSame('sub_9', $replacement->getStripeSubscriptionId());
    }

    private function makeKey(): ApiKey
    {
        return new ApiKey(new User(), 'test', str_repeat('a', 64), 'lodb_aaaaaaa');
    }

    /** Credits are normally mutated by SQL (go-api / addCredits); tests inject via reflection. */
    private function setCredits(ApiKey $key, int $credits): void
    {
        $property = new \ReflectionProperty(ApiKey::class, 'creditsBalance');
        $property->setValue($key, $credits);
    }
}
