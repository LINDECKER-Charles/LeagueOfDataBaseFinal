<?php
declare(strict_types=1);

namespace App\Tests\Unit\Validator;

use App\Validator\CnilPassword;
use App\Validator\CnilPasswordValidator;
use PHPUnit\Framework\Attributes\DataProvider;
use Symfony\Component\Validator\Exception\UnexpectedValueException;
use Symfony\Component\Validator\Test\ConstraintValidatorTestCase;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * The stub translator echoes the key, so assertions read as message keys.
 * Policy under test: CNIL 2022-100 — >= 12 chars, 4 character classes, and the
 * embedded common-password denylist (case-insensitive).
 *
 * @extends ConstraintValidatorTestCase<CnilPasswordValidator>
 */
final class CnilPasswordValidatorTest extends ConstraintValidatorTestCase
{
    protected function createValidator(): CnilPasswordValidator
    {
        $echoTranslator = new class implements TranslatorInterface {
            public function trans(?string $id, array $parameters = [], ?string $domain = null, ?string $locale = null): string
            {
                return (string) $id;
            }

            public function getLocale(): string
            {
                return 'en';
            }
        };

        return new CnilPasswordValidator($echoTranslator);
    }

    public function testCompliantPasswordRaisesNothing(): void
    {
        $this->validator->validate('Str0ng-passphrase!', new CnilPassword());

        $this->assertNoViolation();
    }

    public function testEmptyValuesAreLeftToNotBlank(): void
    {
        $this->validator->validate(null, new CnilPassword());
        $this->validator->validate('', new CnilPassword());

        $this->assertNoViolation();
    }

    public function testNonStringIsRejected(): void
    {
        $this->expectException(UnexpectedValueException::class);

        $this->validator->validate(123, new CnilPassword());
    }

    #[DataProvider('provideSingleMissingCriterion')]
    public function testEachMissingCriterionIsReportedAlone(string $password, string $expectedKey): void
    {
        $this->validator->validate($password, new CnilPassword());

        $this->buildViolation($expectedKey)->assertRaised();
    }

    /** @return iterable<string, array{string, string}> */
    public static function provideSingleMissingCriterion(): iterable
    {
        yield 'too short' => ['Sh0rt-pass!', 'auth.password.rule_length'];
        yield 'no lowercase' => ['NOLOWERCASE-123456', 'auth.password.rule_lowercase'];
        yield 'no uppercase' => ['nouppercase-123456', 'auth.password.rule_uppercase'];
        yield 'no digit' => ['NoDigits-Password!', 'auth.password.rule_digit'];
        yield 'no special' => ['NoSpecials12345678', 'auth.password.rule_special'];
    }

    public function testUnicodeSatisfiesCaseAndSpecialClasses(): void
    {
        // é/É are letters, the em-dash and space count as specials (wide set).
        $this->validator->validate('Épée légère — 2026', new CnilPassword());

        $this->assertNoViolation();
    }

    public function testCommonPasswordIsRejectedCaseInsensitively(): void
    {
        // 'trustno1' sits in the denylist; the mixed-case variant must hit it too.
        $this->validator->validate('TrustNo1', new CnilPassword());

        $this->buildViolation('auth.password.rule_length')
            ->buildNextViolation('auth.password.rule_special')
            ->buildNextViolation('auth.password.too_common')
            ->assertRaised();
    }
}
