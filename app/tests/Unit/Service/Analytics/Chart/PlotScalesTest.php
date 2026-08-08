<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Analytics\Chart;

use App\Service\Analytics\Chart\PlotScales;
use App\Service\Analytics\Chart\SvgPrimitives;
use PHPUnit\Framework\TestCase;

final class PlotScalesTest extends TestCase
{
    private const DELTA = 0.001;

    public function testSeriesSpansTheWholePlotWidth(): void
    {
        $scales = new PlotScales(5, 10.0);

        self::assertEqualsWithDelta(SvgPrimitives::PAD_X, $scales->x(0), self::DELTA);
        self::assertEqualsWithDelta(
            SvgPrimitives::PAD_X + SvgPrimitives::PLOT_W,
            $scales->x(4),
            self::DELTA,
        );
    }

    public function testALonePointSitsInTheMiddleOfTheBox(): void
    {
        $scales = new PlotScales(1, 10.0);

        self::assertEqualsWithDelta(
            SvgPrimitives::PAD_X + SvgPrimitives::PLOT_W / 2,
            $scales->x(0),
            self::DELTA,
        );
    }

    public function testValuesAreInvertedSoTheMaximumSitsAtTheTop(): void
    {
        $scales = new PlotScales(3, 10.0);

        self::assertEqualsWithDelta(SvgPrimitives::PAD_TOP, $scales->y(10.0), self::DELTA);
        self::assertEqualsWithDelta($scales->baseline(), $scales->y(0.0), self::DELTA);
        self::assertGreaterThan($scales->y(10.0), $scales->y(5.0));
    }

    /** A flat series must not divide by zero: every point lands on the baseline. */
    public function testAnAllZeroSeriesCollapsesOntoTheBaseline(): void
    {
        $scales = new PlotScales(3, 0.0);

        self::assertEqualsWithDelta($scales->baseline(), $scales->y(0.0), self::DELTA);
        self::assertEqualsWithDelta(
            SvgPrimitives::PAD_TOP + SvgPrimitives::PLOT_H,
            $scales->baseline(),
            self::DELTA,
        );
    }
}
