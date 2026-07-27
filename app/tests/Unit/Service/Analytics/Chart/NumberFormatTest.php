<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics\Chart;

use App\Service\Analytics\Chart\NumberFormat;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

final class NumberFormatTest extends TestCase
{
    private NumberFormat $format;

    protected function setUp(): void
    {
        $this->format = new NumberFormat();
    }

    #[DataProvider('byteSizes')]
    public function testBytesFormatting(int $bytes, string $expected): void
    {
        self::assertSame($expected, $this->format->bytes($bytes));
    }

    public static function byteSizes(): array
    {
        return [
            [0, '0 B'],
            [512, '512 B'],
            [1024, '1.00 KB'],
            [1048576, '1.00 MB'],
            [1610612736, '1.50 GB'],
        ];
    }

    #[DataProvider('compactNumbers')]
    public function testCompactFormatting(int $n, string $expected): void
    {
        self::assertSame($expected, $this->format->compact($n));
    }

    public static function compactNumbers(): array
    {
        return [[7, '7'], [999, '999'], [1500, '1.5k'], [12000, '12k'], [2500000, '2.5M']];
    }

    public function testIntegerUsesNarrowThousandSeparator(): void
    {
        self::assertSame('1 234 567', $this->format->integer(1234567));
    }
}
