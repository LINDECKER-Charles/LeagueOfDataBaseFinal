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
    private const DOT_RADIUS = 2.5;
    private const GRID_STEPS = [0.0, 0.5, 1.0];
    /** Half-width of the invisible hover target around each point. */
    private const HIT_HALF_WIDTH = 6.0;
    /** Gap between a Y grid line and its right-aligned label. */
    private const Y_LABEL_GAP = 6;
    /** Nudges the Y label onto the grid line's optical centre. */
    private const Y_LABEL_BASELINE = 3;
    /** Drop of the X labels below the baseline, clearing the axis. */
    private const X_LABEL_DROP = 16;
    /** Enough of the payload digest to make the clip-path id unique on a page. */
    private const CLIP_ID_LENGTH = 10;
    /** Strips the year from an ISO date, leaving the MM-DD tick label. */
    private const MONTH_DAY_OFFSET = 5;

    public function __construct(
        private readonly SvgPrimitives $svg,
        private readonly NumberFormat $format,
    ) {}

    /**
     * @param list<array{date: string, ...}> $series
     * @param list<array{key: string, label: string, color: string, format?: string}> $lines
     */
    public function render(
        array $series,
        array $lines,
        string $ariaLabel = 'Série temporelle',
    ): string {
        if ($series === []) {
            return $this->svg->emptyChart($ariaLabel);
        }

        $max = $this->seriesMax($series, $lines);
        $scales = new PlotScales(count($series), $max);
        $payload = $this->payload($series, $lines, $max);
        $clipId = 'cp-' . substr(sha1($payload), 0, self::CLIP_ID_LENGTH);

        $body = $this->defs($clipId)
            . $this->gridY($max, $scales)
            . sprintf(
                '<g class="c-plot" clip-path="url(#%s)">%s</g>',
                $clipId,
                $this->marks($series, $lines, $scales),
            )
            . sprintf('<g class="c-xaxis">%s</g>', $this->axisX($series, $scales));

        return sprintf(
            '<figure class="chart-fig" data-chart="%s">%s</figure>',
            $this->svg->esc($payload),
            $this->svg->svg($body, $ariaLabel),
        );
    }

    private function defs(string $clipId): string
    {
        return sprintf(
            '<defs><clipPath id="%s">'
            . '<rect x="%d" y="%d" width="%d" height="%d"/>'
            . '</clipPath></defs>',
            $clipId,
            SvgPrimitives::PAD_X,
            SvgPrimitives::PAD_TOP,
            SvgPrimitives::PLOT_W,
            SvgPrimitives::PLOT_H,
        );
    }

    /**
     * @param list<array{date: string, ...}> $series
     * @param list<array{key: string, label: string, color: string, format?: string}> $lines
     */
    private function marks(array $series, array $lines, PlotScales $scales): string
    {
        $out = '';
        foreach ($lines as $idx => $line) {
            $points = [];
            foreach ($series as $i => $row) {
                $points[] = [$scales->x($i), $scales->y((float) ($row[$line['key']] ?? 0))];
            }
            // Only the first series gets the filled area: stacked fills would
            // read as a part-to-whole relation the data does not carry.
            $out .= $idx === 0
                ? $this->svg->area($points, $scales->baseline(), $line['color'])
                : '';
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
                ? sprintf(
                    '<circle cx="%.1f" cy="%.1f" r="%s" fill="%s"/>',
                    $p[0],
                    $p[1],
                    self::DOT_RADIUS,
                    $line['color'],
                )
                : '';
            $out .= sprintf(
                '<g class="c-dot">'
                . '<rect x="%.1f" y="%d" width="%.1f" height="%d" fill="transparent"/>%s'
                . '<title>%s — %s : %s</title></g>',
                $p[0] - self::HIT_HALF_WIDTH,
                SvgPrimitives::PAD_TOP,
                self::HIT_HALF_WIDTH * 2,
                SvgPrimitives::PLOT_H,
                $marker,
                $this->svg->esc((string) $series[$i]['date']),
                $this->svg->esc($line['label']),
                $this->format->integer((float) ($series[$i][$line['key']] ?? 0)),
            );
        }

        return $out;
    }

    private function gridY(float $max, PlotScales $scales): string
    {
        $out = '';
        foreach (self::GRID_STEPS as $step) {
            $value = $max * $step;
            $at = $scales->y($value);
            $out .= sprintf(
                '<line x1="%d" y1="%.1f" x2="%d" y2="%.1f" class="c-grid"/>',
                SvgPrimitives::PAD_X,
                $at,
                SvgPrimitives::PAD_X + SvgPrimitives::PLOT_W,
                $at,
            );
            $out .= sprintf(
                '<text x="%d" y="%.1f" class="c-axis" text-anchor="end">%s</text>',
                SvgPrimitives::PAD_X - self::Y_LABEL_GAP,
                $at + self::Y_LABEL_BASELINE,
                $this->format->compact($value),
            );
        }

        return $out;
    }

    /** @param list<array{date: string, ...}> $series */
    private function axisX(array $series, PlotScales $scales): string
    {
        $n = count($series);
        $ticks = array_values(array_unique([0, intdiv($n - 1, 2), $n - 1]));
        $labelY = $scales->baseline() + self::X_LABEL_DROP;
        $out = '';
        foreach ($ticks as $i) {
            $label = substr((string) ($series[$i]['date'] ?? ''), self::MONTH_DAY_OFFSET);
            $anchor = $i === 0 ? 'start' : ($i === $n - 1 ? 'end' : 'middle');
            $out .= sprintf(
                '<text x="%.1f" y="%.1f" class="c-axis" text-anchor="%s">%s</text>',
                $scales->x($i), $labelY, $anchor, $this->svg->esc($label),
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
        return json_encode([
            'box' => [
                'w' => SvgPrimitives::W,
                'h' => SvgPrimitives::H,
                'padX' => SvgPrimitives::PAD_X,
                'padTop' => SvgPrimitives::PAD_TOP,
                'plotW' => SvgPrimitives::PLOT_W,
                'plotH' => SvgPrimitives::PLOT_H,
            ],
            'max' => $max,
            'dates' => array_map(
                static fn (array $row): string => (string) ($row['date'] ?? ''),
                $series,
            ),
            'series' => $this->plottedLines($series, $lines),
        ], JSON_THROW_ON_ERROR | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    }

    /**
     * One entry per line: its legend, and its raw values in series order.
     *
     * @param list<array{date: string, ...}> $series
     * @param list<array{key: string, label: string, color: string, format?: string}> $lines
     * @return list<array{label: string, color: string, format: string, values: list<float>}>
     */
    private function plottedLines(array $series, array $lines): array
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

        return $plotted;
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
