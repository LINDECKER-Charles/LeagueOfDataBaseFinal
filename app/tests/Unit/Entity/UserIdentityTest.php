<?php
declare(strict_types=1);

namespace App\Tests\Unit\Entity;

use App\Entity\User;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;
use Symfony\Component\Validator\Validation;
use Symfony\Component\Validator\Validator\ValidatorInterface;

/**
 * Riot ID surface of the account: the tagline format (3-5 alphanumerics) and
 * the username#TAG display name. Property-level validation only — uniqueness
 * (class-level, Doctrine-backed) is out of unit scope.
 */
final class UserIdentityTest extends TestCase
{
    private ValidatorInterface $validator;

    protected function setUp(): void
    {
        $this->validator = Validation::createValidatorBuilder()
            ->enableAttributeMapping()
            ->getValidator();
    }

    public function testDisplayNameIsUsernameAlone(): void
    {
        $user = (new User())->setUsername('Faker');

        self::assertSame('Faker', $user->displayName());
    }

    public function testDisplayNameAppendsTheTagline(): void
    {
        $user = (new User())->setUsername('Faker')->setRiotTagline('KR1');

        self::assertSame('Faker#KR1', $user->displayName());
    }

    #[DataProvider('provideValidTaglines')]
    public function testValidTaglinesPass(?string $tagline): void
    {
        self::assertCount(0, $this->validator->validatePropertyValue(User::class, 'riotTagline', $tagline));
    }

    /** @return iterable<string, array{?string}> */
    public static function provideValidTaglines(): iterable
    {
        yield 'unset' => [null];
        yield 'region style' => ['EUW'];
        yield 'digits only' => ['123'];
        yield 'mixed, max length' => ['Ab12C'];
    }

    #[DataProvider('provideInvalidTaglines')]
    public function testInvalidTaglinesAreRejected(string $tagline): void
    {
        self::assertGreaterThan(
            0,
            \count($this->validator->validatePropertyValue(User::class, 'riotTagline', $tagline)),
        );
    }

    /** @return iterable<string, array{string}> */
    public static function provideInvalidTaglines(): iterable
    {
        yield 'too short' => ['ab'];
        yield 'too long' => ['ABCDEF'];
        yield 'hash inside' => ['E#W'];
        yield 'non ascii' => ['éàç'];
        yield 'space' => ['EU W'];
    }
}
