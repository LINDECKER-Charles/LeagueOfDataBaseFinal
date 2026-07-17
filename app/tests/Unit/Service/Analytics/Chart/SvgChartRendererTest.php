<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics\Chart;

use App\Service\Analytics\Chart\SvgChartRenderer;
use PHPUnit\Framework\Attributes\DataProvider;
use PHPUnit\Framework\TestCase;

final class SvgChartRendererTest extends TestCase
{
    private SvgChartRenderer $charts;

    protected function setUp(): void
    {
        $this->charts = new SvgChartRenderer();
    }

    #[DataProvider('byteSizes')]
    public function testBytesFormatting(int $bytes, string $expected): void
    {
        self::assertSame($expected, $this->charts->bytes($bytes));
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
        self::assertSame($expected, $this->charts->compact($n));
    }

    public static function compactNumbers(): array
    {
        return [[7, '7'], [999, '999'], [1500, '1.5k'], [12000, '12k'], [2500000, '2.5M']];
    }

    public function testHeatColorIsTrackWhenZero(): void
    {
        self::assertSame('var(--track)', $this->charts->heatColor(0, 100));
        self::assertSame('var(--track)', $this->charts->heatColor(5, 0));
    }

    public function testHeatColorScalesWithValue(): void
    {
        self::assertStringContainsString('rgba(10, 200, 185', $this->charts->heatColor(50, 100));
    }

    public function testTimeSeriesRendersSvgWithMarks(): void
    {
        $series = [
            ['date' => '2026-07-15', 'views' => 4, 'visitors' => 2],
            ['date' => '2026-07-16', 'views' => 9, 'visitors' => 5],
        ];
        $svg = $this->charts->timeSeries($series, [['key' => 'views', 'label' => 'Vues', 'color' => 'var(--gold)']]);

        self::assertStringStartsWith('<svg', $svg);
        self::assertStringContainsString('<polyline', $svg);
        self::assertStringContainsString('<title>', $svg);
    }

    public function testTimeSeriesEmptyStateWhenNoData(): void
    {
        self::assertStringContainsString('Aucune donnée', $this->charts->timeSeries([], []));
    }

    public function testDonutRendersOneArcPerSlice(): void
    {
        $slices = [
            ['name' => 'A', 'value' => 3, 'color' => 'var(--gold)'],
            ['name' => 'B', 'value' => 1, 'color' => 'var(--hex)'],
        ];
        $svg = $this->charts->donut($slices, '4', 'vues');

        self::assertSame(3, substr_count($svg, '<circle')); // 1 track + 2 slices
        self::assertStringContainsString('stroke-dasharray', $svg);
    }

    public function testDonutEmptyWhenTotalZero(): void
    {
        self::assertStringContainsString('Aucune donnée', $this->charts->donut([['name' => 'A', 'value' => 0, 'color' => 'x']]));
    }

    public function testSparklineNeedsTwoPoints(): void
    {
        self::assertSame('', $this->charts->sparkline([5]));
        self::assertStringContainsString('<svg', $this->charts->sparkline([1, 2, 3]));
    }

    public function testTextIsEscaped(): void
    {
        $svg = $this->charts->donut([['name' => 'A & <b>', 'value' => 1, 'color' => 'x']], 'x', 'y');

        self::assertStringNotContainsString('<b>', $svg);
        self::assertStringContainsString('&amp;', $svg);
    }
}
