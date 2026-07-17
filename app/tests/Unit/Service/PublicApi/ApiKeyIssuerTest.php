<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\PublicApi;

use App\Entity\ApiKey;
use App\Entity\ApiPlan;
use App\Entity\User;
use App\Service\PublicApi\ApiKeyIssuer;
use PHPUnit\Framework\TestCase;

final class ApiKeyIssuerTest extends TestCase
{
    private ApiKeyIssuer $issuer;

    protected function setUp(): void
    {
        $this->issuer = new ApiKeyIssuer();
    }

    public function testRawKeyMatchesTheGoApiContractShape(): void
    {
        $issued = $this->issuer->issue(new User());

        // "lodb_" + 40 lowercase hex chars — exactly what go-api's keys.Hash accepts.
        self::assertMatchesRegularExpression('/^lodb_[0-9a-f]{40}$/', $issued->rawKey);
    }

    public function testStoredHashIsSha256OfTheFullRawKey(): void
    {
        $issued = $this->issuer->issue(new User());

        self::assertSame(hash('sha256', $issued->rawKey), $issued->key->getKeyHash());
    }

    public function testDisplayPrefixIsTheFirstTwelveCharacters(): void
    {
        $issued = $this->issuer->issue(new User());

        self::assertSame(substr($issued->rawKey, 0, 12), $issued->key->getKeyPrefix());
        self::assertSame(12, \strlen($issued->key->getKeyPrefix()));
    }

    public function testTwoIssuesNeverCollide(): void
    {
        $user = new User();

        self::assertNotSame(
            $this->issuer->issue($user)->key->getKeyHash(),
            $this->issuer->issue($user)->key->getKeyHash(),
        );
    }

    public function testNameIsTrimmedTruncatedAndDefaulted(): void
    {
        $user = new User();

        self::assertSame('default', $this->issuer->issue($user, '   ')->key->getName());
        self::assertSame('my-app', $this->issuer->issue($user, '  my-app  ')->key->getName());
        self::assertSame(
            ApiKey::NAME_MAX_LENGTH,
            mb_strlen($this->issuer->issue($user, str_repeat('x', 100))->key->getName()),
        );
    }

    public function testRegenerateRevokesThePreviousKeyAndCarriesEntitlements(): void
    {
        $previous = $this->issuer->issue(new User(), 'prod')->key;
        $previous->applyPlan(ApiPlan::Monthly);
        $previous->attachStripe('cus_42', 'sub_42');

        $issued = $this->issuer->regenerate($previous);

        self::assertFalse($previous->isActive());
        self::assertNotNull($previous->getRevokedAt());
        self::assertTrue($issued->key->isActive());
        self::assertSame('prod', $issued->key->getName());
        self::assertSame(ApiPlan::Monthly, $issued->key->getPlan());
        self::assertSame(ApiPlan::QUOTA_MONTHLY, $issued->key->getMonthlyQuota());
        self::assertSame(ApiPlan::RATE_MONTHLY, $issued->key->getRateLimitPerMin());
        self::assertSame('cus_42', $issued->key->getStripeCustomerId());
        self::assertSame('sub_42', $issued->key->getStripeSubscriptionId());
        self::assertNotSame($previous->getKeyHash(), $issued->key->getKeyHash());
    }
}
