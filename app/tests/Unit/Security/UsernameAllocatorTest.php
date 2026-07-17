<?php
declare(strict_types=1);

namespace App\Tests\Unit\Security;

use App\Entity\User;
use App\Security\UsernameAllocator;
use PHPUnit\Framework\TestCase;

/**
 * OAuth username derivation: every outcome must satisfy User::USERNAME_PATTERN
 * (the /u/{username} route contract) whatever Google sends as a profile name.
 */
final class UsernameAllocatorTest extends TestCase
{
    private UsernameAllocator $allocator;

    protected function setUp(): void
    {
        $this->allocator = new UsernameAllocator();
    }

    public function testTransliteratesThePreferredHint(): void
    {
        $username = $this->allocator->allocate(['Jean Küpper'], static fn (): bool => false);

        self::assertSame('Jean-Kupper', $username);
        self::assertMatchesRegularExpression(User::USERNAME_PATTERN, $username);
    }

    public function testFallsBackToTheNextHintWhenTheFirstIsUnusable(): void
    {
        self::assertSame(
            'charles-lindecker',
            $this->allocator->allocate(['@@', 'charles.lindecker'], static fn (): bool => false),
        );
    }

    public function testFallsBackToDefaultWhenNoHintIsUsable(): void
    {
        self::assertSame('Summoner', $this->allocator->allocate(['', '!!', '🎮🎮'], static fn (): bool => false));
    }

    public function testAppendsNumericSuffixesOnCollision(): void
    {
        $taken = ['Ahri', 'Ahri2', 'Ahri3'];

        self::assertSame(
            'Ahri4',
            $this->allocator->allocate(['Ahri'], static fn (string $c): bool => \in_array($c, $taken, true)),
        );
    }

    public function testLongHintsAreCappedAt24EvenWithSuffix(): void
    {
        $base = str_repeat('a', 40);
        $first = $this->allocator->allocate([$base], static fn (): bool => false);
        $collided = $this->allocator->allocate([$base], static fn (string $c): bool => $c === $first);

        self::assertSame(24, mb_strlen($first));
        self::assertSame(24, mb_strlen($collided));
        self::assertStringEndsWith('2', $collided);
        self::assertMatchesRegularExpression(User::USERNAME_PATTERN, $collided);
    }

    public function testTerminatesWithRandomSuffixWhenSequentialProbesAreExhausted(): void
    {
        // Everything without a >= 6-digit suffix is taken: forces the random branch.
        $username = $this->allocator->allocate(
            ['Ahri'],
            static fn (string $c): bool => preg_match('/\d{6}$/', $c) !== 1,
        );

        self::assertMatchesRegularExpression('/^Ahri\d{6}$/', $username);
        self::assertMatchesRegularExpression(User::USERNAME_PATTERN, $username);
    }
}
