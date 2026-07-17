<?php
declare(strict_types=1);

namespace App\Tests\Unit\Entity;

use App\Entity\ApiPlan;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

/**
 * The pricing grid is a product contract (validated with go-api's monthly
 * quota semantics): these tests pin every plan's quota, rate, price and
 * Stripe interval so a refactor cannot silently reprice the API.
 */
final class ApiPlanTest extends TestCase
{
    /** @return iterable<string, array{ApiPlan, int, int, ?int, ?string}> plan, quota, rate, priceCents, interval */
    public static function planGrid(): iterable
    {
        yield 'free' => [ApiPlan::Free, 500, 10, null, null];
        yield 'credits' => [ApiPlan::Credits, 500, 60, null, null];
        yield 'monthly' => [ApiPlan::Monthly, 15_000, 120, 500, 'month'];
        yield 'monthly_plus' => [ApiPlan::MonthlyPlus, 45_000, 120, 1_500, 'month'];
        yield 'annual' => [ApiPlan::Annual, 20_000, 300, 4_800, 'year'];
        yield 'annual_plus' => [ApiPlan::AnnualPlus, 60_000, 300, 14_400, 'year'];
    }

    #[DataProvider('planGrid')]
    public function testGridMatchesTheActedProductPolicy(
        ApiPlan $plan,
        int $quota,
        int $rate,
        ?int $priceCents,
        ?string $interval,
    ): void {
        self::assertSame($quota, $plan->monthlyQuota());
        self::assertSame($rate, $plan->rateLimitPerMin());
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
