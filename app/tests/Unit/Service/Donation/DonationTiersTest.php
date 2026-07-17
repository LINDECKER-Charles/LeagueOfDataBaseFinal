<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Donation;

use App\Service\Donation\DonationTiers;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

final class DonationTiersTest extends TestCase
{
    #[DataProvider('validAmounts')]
    public function testNormalizesValidEuroInputToCents(string $input, int $expectedCents): void
    {
        self::assertSame($expectedCents, DonationTiers::normalizeEuroInput($input));
    }

    /** @return array<string, array{string, int}> */
    public static function validAmounts(): array
    {
        return [
            'whole euros' => ['3', 300],
            'dot decimal' => ['5.5', 550],
            'french comma' => ['5,50', 550],
            'two decimals' => ['12.34', 1234],
            'single decimal digit expands to tens of cents' => ['7,5', 750],
            'min bound' => ['1', 100],
            'max bound' => ['500', 50000],
            'just under max' => ['499,99', 49999],
            'surrounding whitespace trimmed' => [' 10 ', 1000],
            'leading zeros tolerated' => ['007', 700],
        ];
    }

    #[DataProvider('rejectedAmounts')]
    public function testRejectsInvalidOrOutOfBoundsInput(string $input): void
    {
        self::assertNull(DonationTiers::normalizeEuroInput($input));
    }

    /** @return array<string, array{string}> */
    public static function rejectedAmounts(): array
    {
        return [
            'letters' => ['abc'],
            'empty' => [''],
            'zero' => ['0'],
            'below min' => ['0.99'],
            'above max' => ['1000'],
            'one cent above max' => ['500.01'],
            'negative' => ['-5'],
            'three decimals' => ['5.555'],
            'double separator' => ['5.5.5'],
            'scientific notation' => ['1e3'],
            'thousands space' => ['5 000'],
            'currency symbol' => ['5€'],
        ];
    }

    public function testPresetMembership(): void
    {
        foreach (DonationTiers::PRESETS_CENTS as $preset) {
            self::assertTrue(DonationTiers::isPreset($preset));
        }

        self::assertFalse(DonationTiers::isPreset(0));
        self::assertFalse(DonationTiers::isPreset(299));
        self::assertFalse(DonationTiers::isPreset(-500));
    }

    public function testEveryPresetSitsWithinTheAcceptedBounds(): void
    {
        foreach (DonationTiers::PRESETS_CENTS as $preset) {
            self::assertTrue(DonationTiers::isWithinBounds($preset));
        }
    }
}
