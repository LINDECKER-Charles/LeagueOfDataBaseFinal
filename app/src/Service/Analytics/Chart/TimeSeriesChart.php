<?php
declare(strict_types=1);

namespace App\Service\Analytics\Chart;

/**
 * Overlaid line/area series over a shared date axis (one axis only).
 *
 * The SVG is server-rendered and readable on its own: grid, axes, filled area
 * and a per-point `<title>` give a working chart with JavaScript disabled. The
 * same markup doubles as the substrate for the client enhancement
 * (public/admin/js/chart.js): the marks live in a clipped `.c-plot` group the
 * script transforms for zoom/pan, and the raw values travel alongside in a
 * `data-chart` JSON payload so the tooltip reads exact numbers instead of
 * re-deriving them from geometry.
 */
final class TimeSeriesChart
{
    /** Above this point count the per-point markers turn to visual noise. */
    private const DOT_LIMIT = 45;
    private const GRID_STEPS = [0.0, 0.5, 1.0];
    /** Half-width of the invisible hover target around each point. */
    private const HIT_HALF_WIDTH = 6.0;

    private const PLOT_W = SvgPrimitives::W - 2 * SvgPrimitives::PAD_X;
    private const PLOT_H = SvgPrimitives::H - SvgPrimitives::PAD_TOP - SvgPrimitives::PAD_BOTTOM;

    public function __construct(
        private readonly SvgPrimitives $svg,
        private readonly NumberFormat $format,
    ) {}

    /**
     * @param list<array{date: string, ...}> $series
     * @param list<array{key: string, label: string, color: string, format?: string}> $lines
     */
    public function render(array $series, array $lines, string $ariaLabel = 'Série temporelle'): string
    {
        if ($series === []) {
            return $this->svg->empty($ariaLabel);
        }

        $max = $this->seriesMax($series, $lines);
        $x = $this->xScale(count($series));
        $y = $this->yScale($max);
        $payload = $this->payload($series, $lines, $max);
        $clipId = 'cp-' . substr(sha1($payload), 0, 10);

        $body = $this->defs($clipId)
            . $this->gridY($max, $y)
            . sprintf('<g class="c-plot" clip-path="url(#%s)">%s</g>', $clipId, $this->marks($series, $lines, $x, $y))
            . sprintf('<g class="c-xaxis">%s</g>', $this->axisX($series, $x, $y(0.0)));

        return sprintf(
            '<figure class="chart-fig" data-chart="%s">%s</figure>',
            $this->svg->esc($payload),
            $this->svg->svg($body, $ariaLabel),
        );
    }

    private function defs(string $clipId): string
    {
        return sprintf(
            '<defs><clipPath id="%s"><rect x="%d" y="%d" width="%d" height="%d"/></clipPath></defs>',
            $clipId, SvgPrimitives::PAD_X, SvgPrimitives::PAD_TOP, self::PLOT_W, self::PLOT_H,
        );
    }

    /**
     * @param list<array{date: string, ...}> $series
     * @param list<array{key: string, label: string, color: string, format?: string}> $lines
     */
    private function marks(array $series, array $lines, \Closure $x, \Closure $y): string
    {
        $out = '';
        foreach ($lines as $idx => $line) {
            $points = [];
            foreach ($series as $i => $row) {
                $points[] = [$x($i), $y((float) ($row[$line['key']] ?? 0))];
            }
            // Only the first series gets the filled area: stacked fills would
            // read as a part-to-whole relation the data does not carry.
            $out .= $idx === 0 ? $this->svg->area($points, $y(0.0), $line['color']) : '';
            $out .= $this->svg->polyline($points, $line['color']);
            $out .= $this->dots($points, $series, $line);
        }

        return $out;
    }

    /**
     * @param list<array{0: float, 1: float}> $points
     * @param list<array{date: string, ...}> $series
     * @param array{key: string, label: string, color: string, format?: string} $line
     */
    private function dots(array $points, array $series, array $line): string
    {
        $out = '';
        $showMarkers = count($points) <= self::DOT_LIMIT;
        foreach ($points as $i => $p) {
            $marker = $showMarkers
                ? sprintf('<circle cx="%.1f" cy="%.1f" r="2.5" fill="%s"/>', $p[0], $p[1], $line['color'])
                : '';
            $out .= sprintf(
                '<g class="c-dot"><rect x="%.1f" y="%d" width="%.1f" height="%d" fill="transparent"/>%s<title>%s — %s : %s</title></g>',
                $p[0] - self::HIT_HALF_WIDTH, SvgPrimitives::PAD_TOP, self::HIT_HALF_WIDTH * 2, self::PLOT_H, $marker,
                $this->svg->esc((string) $series[$i]['date']),
                $this->svg->esc($line['label']),
                $this->format->integer((float) ($series[$i][$line['key']] ?? 0)),
            );
        }

        return $out;
    }

    private function gridY(float $max, \Closure $y): string
    {
        $out = '';
        foreach (self::GRID_STEPS as $step) {
            $value = $max * $step;
            $at = $y($value);
            $out .= sprintf(
                '<line x1="%d" y1="%.1f" x2="%d" y2="%.1f" class="c-grid"/>',
                SvgPrimitives::PAD_X, $at, SvgPrimitives::PAD_X + self::PLOT_W, $at,
            );
            $out .= sprintf(
                '<text x="%d" y="%.1f" class="c-axis" text-anchor="end">%s</text>',
                SvgPrimitives::PAD_X - 6, $at + 3, $this->format->compact($value),
            );
        }

        return $out;
    }

    /** @param list<array{date: string, ...}> $series */
    private function axisX(array $series, \Closure $x, float $baseY): string
    {
        $n = count($series);
        $ticks = array_values(array_unique([0, intdiv($n - 1, 2), $n - 1]));
        $out = '';
        foreach ($ticks as $i) {
            $label = substr((string) ($series[$i]['date'] ?? ''), 5); // MM-DD
            $anchor = $i === 0 ? 'start' : ($i === $n - 1 ? 'end' : 'middle');
            $out .= sprintf(
                '<text x="%.1f" y="%.1f" class="c-axis" text-anchor="%s">%s</text>',
                $x($i), $baseY + 16, $anchor, $this->svg->esc($label),
            );
        }

        return $out;
    }

    /**
     * Raw values + plot box for the client enhancement. Keeping the numbers here
     * (rather than parsing them back out of the SVG) is what lets the tooltip
     * stay exact while the geometry is zoomed.
     *
     * @param list<array{date: string, ...}> $series
     * @param list<array{key: string, label: string, color: string, format?: string}> $lines
     */
    private function payload(array $series, array $lines, float $max): string
    {
        $plotted = [];
        foreach ($lines as $line) {
            $plotted[] = [
                'label' => $line['label'],
                'color' => $line['color'],
                'format' => $line['format'] ?? 'int',
                'values' => array_map(
                    static fn (array $row): float => (float) ($row[$line['key']] ?? 0),
                    $series,
                ),
            ];
        }

        return json_encode([
            'box' => [
                'w' => SvgPrimitives::W,
                'h' => SvgPrimitives::H,
                'padX' => SvgPrimitives::PAD_X,
                'padTop' => SvgPrimitives::PAD_TOP,
                'plotW' => self::PLOT_W,
                'plotH' => self::PLOT_H,
            ],
            'max' => $max,
            'dates' => array_map(static fn (array $row): string => (string) ($row['date'] ?? ''), $series),
            'series' => $plotted,
        ], JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    }

    private function xScale(int $n): \Closure
    {
        return static fn (int $i): float => SvgPrimitives::PAD_X
            + ($n === 1 ? self::PLOT_W / 2 : $i * self::PLOT_W / ($n - 1));
    }

    private function yScale(float $max): \Closure
    {
        return static fn (float $v): float => SvgPrimitives::PAD_TOP + self::PLOT_H
            - ($max > 0 ? $v / $max : 0) * self::PLOT_H;
    }

    /**
     * @param list<array{date: string, ...}> $series
     * @param list<array{key: string, ...}> $lines
     */
    private function seriesMax(array $series, array $lines): float
    {
        $max = 0.0;
        foreach ($series as $row) {
            foreach ($lines as $line) {
                $max = max($max, (float) ($row[$line['key']] ?? 0));
            }
        }

        return $max;
    }
}
