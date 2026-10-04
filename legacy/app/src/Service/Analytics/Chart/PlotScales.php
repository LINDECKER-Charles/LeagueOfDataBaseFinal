<?php
declare(strict_types=1);

namespace App\Service\Analytics\Chart;

/**
 * Maps a data point onto the plot box of {@see SvgPrimitives}: the index of a
 * point to an X coordinate, a value to a Y coordinate. Both mappings are derived
 * from the same series (its point count and its maximum) and are always used
 * together, so they travel as one object rather than as a pair of loose closures.
 */
final readonly class PlotScales
{
    public function __construct(
        private int $pointCount,
        private float $max,
    ) {}

    /** A lone point sits mid-box: edge-anchored, it would read as a truncated series. */
    public function x(int $index): float
    {
        if ($this->pointCount === 1) {
            return SvgPrimitives::PAD_X + SvgPrimitives::PLOT_W / 2;
        }

        return SvgPrimitives::PAD_X + $index * SvgPrimitives::PLOT_W / ($this->pointCount - 1);
    }

    public function y(float $value): float
    {
        $ratio = $this->max > 0 ? $value / $this->max : 0;

        return SvgPrimitives::PAD_TOP + SvgPrimitives::PLOT_H - $ratio * SvgPrimitives::PLOT_H;
    }

    /** Y of the zero line — the baseline every filled area closes onto. */
    public function baseline(): float
    {
        return $this->y(0.0);
    }
}
