<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Profile;

use App\Service\Profile\ProfilePresenter;
use PHPUnit\Framework\TestCase;

final class ProfilePresenterTest extends TestCase
{
    private ProfilePresenter $presenter;

    protected function setUp(): void
    {
        $this->presenter = new ProfilePresenter();
    }

    public function testMaskEmailKeepsFirstCharAndDomain(): void
    {
        self::assertSame('c***@outlook.fr', $this->presenter->maskEmail('charles@outlook.fr'));
        self::assertSame('a***@b.c', $this->presenter->maskEmail('a@b.c'));
    }

    public function testMaskEmailDegenerateInputsFullyMasked(): void
    {
        self::assertSame('***', $this->presenter->maskEmail('@outlook.fr'));
        self::assertSame('***', $this->presenter->maskEmail('not-an-email'));
        self::assertSame('***', $this->presenter->maskEmail(''));
    }

    public function testMemberSinceAlwaysCarriesTheYear(): void
    {
        $createdAt = new \DateTimeImmutable('2026-07-17 10:00:00');

        // Exact wording depends on ICU availability (full intl vs polyfill vs
        // none) — the contract is "a month/year representation", so assert the
        // invariant part only.
        self::assertStringContainsString('2026', $this->presenter->memberSince($createdAt, 'en'));
        self::assertStringContainsString('2026', $this->presenter->memberSince($createdAt, 'fr'));
    }
}
