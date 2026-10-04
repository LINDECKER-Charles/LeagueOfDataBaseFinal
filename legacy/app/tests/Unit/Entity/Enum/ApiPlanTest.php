<?php
declare(strict_types=1);

namespace App\Tests\Unit\Entity\Enum;

use App\Entity\Enum\ApiPlan;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

/**
 * The pricing grid is a product contract (validated with go-api's monthly
 * quota semantics): these tests pin every plan's quota, rate, price and
 * Stripe interval so a refactor cannot silently reprice the API.
 */
final class ApiPlanTest extends TestCase
{
    /**
     * Each row is: plan, monthly quota, per-minute rate.
     *
     * @return iterable<string, array{ApiPlan, int, int}>
     */
    public static function planAllowances(): iterable
    {
        yield 'free' => [ApiPlan::Free, 500, 10];
        yield 'credits' => [ApiPlan::Credits, 500, 60];
        yield 'monthly' => [ApiPlan::Monthly, 15_000, 120];
        yield 'monthly_plus' => [ApiPlan::MonthlyPlus, 45_000, 120];
        yield 'annual' => [ApiPlan::Annual, 20_000, 300];
        yield 'annual_plus' => [ApiPlan::AnnualPlus, 60_000, 300];
    }

    #[DataProvider('planAllowances')]
    public function testAllowancesMatchTheActedProductPolicy(
        ApiPlan $plan,
        int $quota,
        int $rate,
    ): void {
        self::assertSame($quota, $plan->monthlyQuota());
        self::assertSame($rate, $plan->rateLimitPerMin());
    }

    /**
     * Each row is: plan, price in cents, Stripe billing interval.
     *
     * @return iterable<string, array{ApiPlan, ?int, ?string}>
     */
    public static function planPrices(): iterable
    {
        yield 'free' => [ApiPlan::Free, null, null];
        yield 'credits' => [ApiPlan::Credits, null, null];
        yield 'monthly' => [ApiPlan::Monthly, 500, 'month'];
        yield 'monthly_plus' => [ApiPlan::MonthlyPlus, 1_500, 'month'];
        yield 'annual' => [ApiPlan::Annual, 4_800, 'year'];
        yield 'annual_plus' => [ApiPlan::AnnualPlus, 14_400, 'year'];
    }

    #[DataProvider('planPrices')]
    public function testPricesMatchTheActedProductPolicy(
        ApiPlan $plan,
        ?int $priceCents,
        ?string $interval,
    ): void {
        self::assertSame($priceCents, $plan->priceCents());
        self::assertSame($interval, $plan->stripeInterval());
        self::assertSame($interval !== null, $plan->isSubscription());
    }

    public function testBackedValuesAreTheGoApiContractStrings(): void
    {
        self::assertSame(
            ['free', 'credits', 'monthly', 'monthly_plus', 'annual', 'annual_plus'],
            array_map(static fn (ApiPlan $plan): string => $plan->value, ApiPlan::cases()),
        );
    }

    public function testSubscriptionsListsExactlyThePayablePlans(): void
    {
        self::assertSame(
            [ApiPlan::Monthly, ApiPlan::MonthlyPlus, ApiPlan::Annual, ApiPlan::AnnualPlus],
            ApiPlan::subscriptions(),
        );
        foreach (ApiPlan::subscriptions() as $plan) {
            self::assertTrue($plan->isSubscription());
        }
    }
}
