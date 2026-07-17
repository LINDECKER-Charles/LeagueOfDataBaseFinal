<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\PublicApi;

use App\Entity\ApiPlan;
use App\Service\PublicApi\ApiCheckoutParams;
use App\Service\PublicApi\ApiCreditPack;
use App\Service\PublicApi\CheckoutReturnUrls;
use PHPUnit\Framework\TestCase;

final class ApiCheckoutParamsTest extends TestCase
{
    private const USER_ID = 42;

    private CheckoutReturnUrls $urls;

    protected function setUp(): void
    {
        $this->urls = new CheckoutReturnUrls(
            'https://lodb.example/profile/api?status=ok',
            'https://lodb.example/profile/api?status=cancelled',
        );
    }

    public function testPackBuildsAOneTimePaymentWithTheDispatchMetadata(): void
    {
        $params = ApiCheckoutParams::pack(self::USER_ID, ApiCreditPack::Medium, '10k credits', $this->urls);

        self::assertSame([
            'mode' => 'payment',
            'line_items' => [[
                'quantity' => 1,
                'price_data' => [
                    'currency' => 'eur',
                    'unit_amount' => 1000,
                    'product_data' => ['name' => '10k credits'],
                ],
            ]],
            'success_url' => $this->urls->success,
            'cancel_url' => $this->urls->cancel,
            'client_reference_id' => '42',
            'metadata' => ['kind' => 'api_pack', 'user_id' => '42', 'requests' => '10000'],
        ], $params);
    }

    public function testPlanBuildsARecurringSubscriptionWithTheRightInterval(): void
    {
        $monthly = ApiCheckoutParams::plan(self::USER_ID, ApiPlan::Monthly, 'Monthly', $this->urls);
        $annual = ApiCheckoutParams::plan(self::USER_ID, ApiPlan::AnnualPlus, 'Annual+', $this->urls);

        self::assertSame('subscription', $monthly['mode']);
        self::assertSame(500, $monthly['line_items'][0]['price_data']['unit_amount']);
        self::assertSame(['interval' => 'month'], $monthly['line_items'][0]['price_data']['recurring']);
        self::assertSame(['kind' => 'api_plan', 'user_id' => '42', 'plan' => 'monthly'], $monthly['metadata']);

        self::assertSame(14_400, $annual['line_items'][0]['price_data']['unit_amount']);
        self::assertSame(['interval' => 'year'], $annual['line_items'][0]['price_data']['recurring']);
        self::assertSame('annual_plus', $annual['metadata']['plan']);
    }

    public function testEveryPackChargesOneEuroPerThousandRequests(): void
    {
        foreach (ApiCreditPack::cases() as $pack) {
            self::assertSame($pack->priceCents() * 10, $pack->requests());
        }
        self::assertSame(5_000, ApiCreditPack::Small->requests());
        self::assertSame(10_000, ApiCreditPack::Medium->requests());
        self::assertSame(20_000, ApiCreditPack::Large->requests());
    }

    public function testNonSubscriptionPlansAreRejected(): void
    {
        $this->expectException(\InvalidArgumentException::class);

        ApiCheckoutParams::plan(self::USER_ID, ApiPlan::Free, 'Free', $this->urls);
    }
}
