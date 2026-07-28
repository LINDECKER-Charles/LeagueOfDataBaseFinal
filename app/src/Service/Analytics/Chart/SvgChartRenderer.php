<?php
declare(strict_types=1);

namespace App\Service\Analytics\Chart;

/**
 * Server-rendered, dependency-free SVG for the admin dashboards, and the single
 * entry point the Twig layer talks to (see {@see \App\Twig\AdminChartExtension}).
 *
 * The geometry-heavy time series lives in {@see TimeSeriesChart} — it is the one
 * form the client enhances with zoom/pan, so it carries its own payload and
 * markup contract. The compact forms (donut, sparkline) and the sequential heat
 * colour stay here; flat forms (ranked bars, heatmap grid, matrices) are
 * declarative HTML in the Twig macros.
 *
 * Colours come from the admin's Hextech CSS custom properties (var(--gold),
 * var(--hex)…) so a single stylesheet themes every chart; each data mark carries
 * a `<title>` as the no-JavaScript hover layer.
 */
final class SvgChartRenderer
{
    private const DONUT_RADIUS = 54;
    private const DONUT_STROKE = 20;
    private const DONUT_CX = 90;
    /** Gap between arcs, in path units, so adjacent slices stay legible. */
    private const DONUT_GAP = 2.0;
    private const SPARK_W = 120;
    private const SPARK_H = 30;

    public function __construct(
        private readonly SvgPrimitives $svg,
        private readonly NumberFormat $format,
        private readonly TimeSeriesChart $timeSeriesChart,
    ) {}

    /**
     * @param list<array{date: string, ...}> $series
     * @param list<array{key: string, label: string, color: string, format?: string}> $lines
     */
    public function timeSeries(array $series, array $lines, string $ariaLabel = 'Série temporelle'): string
    {
        return $this->timeSeriesChart->render($series, $lines, $ariaLabel);
    }

    /**
     * Part-to-whole donut via stroke-dashoffset arcs (no trig).
     *
     * @param list<array{name: string, value: int|float, color: string}> $slices
     */
    public function donut(array $slices, string $centerValue = '', string $centerLabel = '', string $ariaLabel = 'Répartition'): string
    {
        $total = array_sum(array_map(static fn (array $s): float => (float) $s['value'], $slices));
        if ($total <= 0) {
            return $this->svg->empty($ariaLabel);
        }

        $circumference = 2 * M_PI * self::DONUT_RADIUS;
        $offset = 0.0;
        $arcs = $this->track();
        foreach ($slices as $slice) {
            $fraction = (float) $slice['value'] / $total;
            $arcs .= $this->arc($slice, $circumference, $fraction, $offset);
            $offset += $fraction * $circumference;
        }

        return $this->svg->svg($arcs . $this->donutCenter($centerValue, $centerLabel), $ariaLabel);
    }

    /**
     * Tiny inline trend line for stat tiles (no axes).
     *
     * @param list<int|float> $values
     */
    public function sparkline(array $values, string $color = 'var(--hex)'): string
    {
        $n = count($values);
        if ($n < 2) {
            return '';
        }
        $min = min($values);
        $span = max($values) - $min ?: 1;
        $points = [];
        foreach (array_values($values) as $i => $v) {
            $points[] = [$i * self::SPARK_W / ($n - 1), self::SPARK_H - ($v - $min) / $span * 26 - 2];
        }

        return sprintf(
            '<svg viewBox="0 0 %d %d" class="sparkline" preserveAspectRatio="none" aria-hidden="true">%s%s</svg>',
            self::SPARK_W, self::SPARK_H,
            $this->svg->area($points, self::SPARK_H, $color, 0.14),
            $this->svg->polyline($points, $color, 1.5),
        );
    }

    /** Sequential cyan ramp (single hue, monotonic) for heatmap cells. */
    public function heatColor(int|float $value, int|float $max): string
    {
        if ($max <= 0 || $value <= 0) {
            return 'var(--track)';
        }
        // Perceptual-ish easing so low counts stay visible.
        $alpha = round(0.10 + 0.90 * min(1.0, $value / $max) ** 0.6, 3);

        return sprintf('rgba(10, 200, 185, %s)', $alpha);
    }

    private function track(): string
    {
        return sprintf(
            '<circle cx="%d" cy="%.1f" r="%d" fill="none" stroke="var(--track)" stroke-width="%d"/>',
            self::DONUT_CX, SvgPrimitives::H / 2, self::DONUT_RADIUS, self::DONUT_STROKE,
        );
    }

    /** @param array{name: string, value: int|float, color: string} $slice */
    private function arc(array $slice, float $circumference, float $fraction, float $offset): string
    {
        $length = max(0.0, $fraction * $circumference - self::DONUT_GAP);
        $cy = SvgPrimitives::H / 2;

        return sprintf(
            '<circle cx="%d" cy="%.1f" r="%d" fill="none" stroke="%s" stroke-width="%d"'
            . ' stroke-dasharray="%.2f %.2f" stroke-dashoffset="%.2f" transform="rotate(-90 %d %.1f)">'
            . '<title>%s — %s (%.1f%%)</title></circle>',
            self::DONUT_CX, $cy, self::DONUT_RADIUS, $slice['color'], self::DONUT_STROKE,
            $length, $circumference - $length, -$offset, self::DONUT_CX, $cy,
            $this->svg->esc($slice['name']), $this->format->integer((float) $slice['value']), $fraction * 100,
        );
    }

    private function donutCenter(string $value, string $label): string
    {
        $cy = SvgPrimitives::H / 2;

        return sprintf(
            '<text x="%d" y="%.1f" text-anchor="middle" class="c-hero">%s</text>'
            . '<text x="%d" y="%.1f" text-anchor="middle" class="c-cap">%s</text>',
            self::DONUT_CX, $cy - 2, $this->svg->esc($value),
            self::DONUT_CX, $cy + 16, $this->svg->esc($label),
        );
    }
}
