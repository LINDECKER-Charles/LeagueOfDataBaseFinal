<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics\Chart;

use App\Service\Analytics\Chart\NumberFormat;
use App\Service\Analytics\Chart\SvgChartRenderer;
use App\Service\Analytics\Chart\SvgPrimitives;
use App\Service\Analytics\Chart\TimeSeriesChart;
use PHPUnit\Framework\TestCase;

final class SvgChartRendererTest extends TestCase
{
    private SvgChartRenderer $charts;

    protected function setUp(): void
    {
        $svg = new SvgPrimitives();
        $format = new NumberFormat();
        $this->charts = new SvgChartRenderer($svg, $format, new TimeSeriesChart($svg, $format));
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

    public function testTimeSeriesIsDelegatedToTheDedicatedChart(): void
    {
        $series = [
            ['date' => '2026-07-15', 'views' => 4],
            ['date' => '2026-07-16', 'views' => 9],
        ];
        $chart = $this->charts->timeSeries(
            $series,
            [['key' => 'views', 'label' => 'Vues', 'color' => 'var(--gold)']],
        );

        self::assertStringContainsString('data-chart=', $chart);
        self::assertStringContainsString('<polyline', $chart);
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
        self::assertStringContainsString(
            'Aucune donnée',
            $this->charts->donut([['name' => 'A', 'value' => 0, 'color' => 'x']]),
        );
    }

    public function testSparklineNeedsTwoPoints(): void
    {
        self::assertSame('', $this->charts->sparkline([5]));
        self::assertStringContainsString('<svg', $this->charts->sparkline([1, 2, 3]));
    }

    public function testTextIsEscaped(): void
    {
        $svg = $this->charts->donut(
            [['name' => 'A & <b>', 'value' => 1, 'color' => 'x']],
            'x',
            'y',
        );

        self::assertStringNotContainsString('<b>', $svg);
        self::assertStringContainsString('&amp;', $svg);
    }
}
