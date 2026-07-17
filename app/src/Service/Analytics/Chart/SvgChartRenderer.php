<?php
declare(strict_types=1);

namespace App\Service\Analytics\Chart;

/**
 * Server-rendered, dependency-free SVG for the admin dashboards. Only the
 * geometry-heavy forms live here (time-series line/area, donut, sparkline) plus
 * the sequential heat colour and number formatters; flat forms (ranked bars,
 * heatmap grid, matrices) are declarative HTML in the Twig macros.
 *
 * Colours come from the admin's Hextech CSS custom properties (var(--gold),
 * var(--hex)…) so a single stylesheet themes every chart; each data mark carries
 * a <title> as the SSR hover layer. Charts scale to their container width via a
 * viewBox (width:100%;height:auto in CSS), preserving aspect ratio.
 */
final class SvgChartRenderer
{
    private const W = 760;
    private const H = 240;
    private const PAD_X = 34;
    private const PAD_TOP = 16;
    private const PAD_BOTTOM = 26;

    /**
     * Overlaid line/area series over a shared date axis (one axis only).
     *
     * @param list<array{date: string, views: int, visitors?: int}> $series
     * @param list<array{key: string, label: string, color: string}> $lines
     */
    public function timeSeries(array $series, array $lines, string $ariaLabel = 'Série temporelle'): string
    {
        $n = count($series);
        if ($n === 0) {
            return $this->empty($ariaLabel);
        }

        $max = $this->seriesMax($series, $lines);
        $plotW = self::W - 2 * self::PAD_X;
        $plotH = self::H - self::PAD_TOP - self::PAD_BOTTOM;
        $x = static fn (int $i): float => self::PAD_X + ($n === 1 ? $plotW / 2 : $i * $plotW / ($n - 1));
        $y = static fn (float $v): float => self::PAD_TOP + $plotH - ($max > 0 ? $v / $max : 0) * $plotH;

        $body = $this->gridY($max, $plotW, $y);
        foreach ($lines as $idx => $line) {
            $points = [];
            foreach ($series as $i => $row) {
                $points[] = [$x($i), $y((float) ($row[$line['key']] ?? 0))];
            }
            $body .= $idx === 0 ? $this->area($points, $y(0), $line['color']) : '';
            $body .= $this->polyline($points, $line['color']);
            $body .= $this->dots($points, $series, $line);
        }
        $body .= $this->axisX($series, $x, $y(0));

        return $this->svg($body, $ariaLabel);
    }

    /**
     * Part-to-whole donut via stroke-dashoffset arcs (no trig, 2px gaps).
     *
     * @param list<array{name: string, value: int|float, color: string}> $slices
     */
    public function donut(array $slices, string $centerValue = '', string $centerLabel = '', string $ariaLabel = 'Répartition'): string
    {
        $total = array_sum(array_map(static fn (array $s): float => (float) $s['value'], $slices));
        if ($total <= 0) {
            return $this->empty($ariaLabel);
        }

        $r = 54;
        $c = 2 * M_PI * $r;
        $cx = 90;
        $cy = self::H / 2;
        $offset = 0.0;
        $arcs = sprintf('<circle cx="%d" cy="%.1f" r="%d" fill="none" stroke="var(--track)" stroke-width="20"/>', $cx, $cy, $r);
        foreach ($slices as $slice) {
            $frac = (float) $slice['value'] / $total;
            $len = max(0.0, $frac * $c - 2);
            $arcs .= sprintf(
                '<circle cx="%d" cy="%.1f" r="%d" fill="none" stroke="%s" stroke-width="20"'
                . ' stroke-dasharray="%.2f %.2f" stroke-dashoffset="%.2f" transform="rotate(-90 %d %.1f)">'
                . '<title>%s — %s (%.1f%%)</title></circle>',
                $cx, $cy, $r, $slice['color'], $len, $c - $len, -$offset, $cx, $cy,
                $this->esc($slice['name']), $this->num((float) $slice['value']), $frac * 100,
            );
            $offset += $frac * $c;
        }
        $center = sprintf(
            '<text x="%d" y="%.1f" text-anchor="middle" class="c-hero">%s</text>'
            . '<text x="%d" y="%.1f" text-anchor="middle" class="c-cap">%s</text>',
            $cx, $cy - 2, $this->esc($centerValue), $cx, $cy + 16, $this->esc($centerLabel),
        );

        return $this->svg($arcs . $center, $ariaLabel);
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
        $max = max($values);
        $min = min($values);
        $span = $max - $min ?: 1;
        $points = [];
        foreach (array_values($values) as $i => $v) {
            $points[] = [$i * 120 / ($n - 1), 30 - ($v - $min) / $span * 26 - 2];
        }

        return sprintf(
            '<svg viewBox="0 0 120 30" class="sparkline" preserveAspectRatio="none" aria-hidden="true">%s%s</svg>',
            $this->area($points, 30, $color, 0.14),
            $this->polyline($points, $color, 1.5),
        );
    }

    /** Sequential cyan ramp (single hue, monotonic) for heatmap cells. */
    public function heatColor(int|float $value, int|float $max): string
    {
        if ($max <= 0 || $value <= 0) {
            return 'var(--track)';
        }
        $t = min(1.0, $value / $max);
        // Perceptual-ish easing so low counts stay visible.
        $alpha = round(0.10 + 0.90 * $t ** 0.6, 3);

        return sprintf('rgba(10, 200, 185, %s)', $alpha);
    }

    // --- number formatters (pure; surfaced as Twig filters) -----------------

    public function bytes(int|float $n): string
    {
        $units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
        $i = 0;
        $n = (float) $n;
        while ($n >= 1024 && $i < count($units) - 1) {
            $n /= 1024;
            $i++;
        }

        return ($i === 0 ? (string) (int) $n : number_format($n, 2)) . ' ' . $units[$i];
    }

    public function compact(int|float $n): string
    {
        $n = (float) $n;
        if ($n >= 1_000_000) {
            return rtrim(rtrim(number_format($n / 1_000_000, 1), '0'), '.') . 'M';
        }
        if ($n >= 1_000) {
            return rtrim(rtrim(number_format($n / 1_000, 1), '0'), '.') . 'k';
        }

        return (string) (int) $n;
    }

    // --- private geometry ---------------------------------------------------

    /**
     * @param list<array{0: float, 1: float}> $points
     */
    private function polyline(array $points, string $color, float $width = 2): string
    {
        return sprintf(
            '<polyline fill="none" stroke="%s" stroke-width="%s" stroke-linejoin="round" stroke-linecap="round" points="%s"/>',
            $color, $width, $this->points($points),
        );
    }

    /**
     * @param list<array{0: float, 1: float}> $points
     */
    private function area(array $points, float $baseline, string $color, float $opacity = 0.12): string
    {
        if ($points === []) {
            return '';
        }
        $first = $points[0];
        $last = $points[count($points) - 1];

        return sprintf(
            '<polygon fill="%s" fill-opacity="%s" stroke="none" points="%.1f,%.1f %s %.1f,%.1f"/>',
            $color, $opacity, $first[0], $baseline, $this->points($points), $last[0], $baseline,
        );
    }

    /**
     * @param list<array{0: float, 1: float}> $points
     * @param list<array{date: string}> $series
     * @param array{key: string, label: string, color: string} $line
     */
    private function dots(array $points, array $series, array $line): string
    {
        $out = '';
        $show = count($points) <= 45;
        foreach ($points as $i => $p) {
            $value = $series[$i][$line['key']] ?? 0;
            $marker = $show ? sprintf('<circle cx="%.1f" cy="%.1f" r="2.5" fill="%s"/>', $p[0], $p[1], $line['color']) : '';
            $out .= sprintf(
                '<g class="c-dot"><rect x="%.1f" y="%d" width="%.1f" height="%d" fill="transparent"/>%s<title>%s — %s : %s</title></g>',
                $p[0] - 6, self::PAD_TOP, 12, self::H - self::PAD_TOP - self::PAD_BOTTOM, $marker,
                $this->esc((string) $series[$i]['date']), $this->esc($line['label']), $this->num((float) $value),
            );
        }

        return $out;
    }

    private function gridY(float $max, float $plotW, callable $y): string
    {
        $out = '';
        foreach ([0.0, 0.5, 1.0] as $t) {
            $value = $max * $t;
            $yy = $y($value);
            $out .= sprintf('<line x1="%d" y1="%.1f" x2="%.1f" y2="%.1f" class="c-grid"/>', self::PAD_X, $yy, self::PAD_X + $plotW, $yy);
            $out .= sprintf('<text x="%d" y="%.1f" class="c-axis" text-anchor="end">%s</text>', self::PAD_X - 6, $yy + 3, $this->compact($value));
        }

        return $out;
    }

    /**
     * @param list<array{date: string}> $series
     */
    private function axisX(array $series, callable $x, float $baseY): string
    {
        $n = count($series);
        $ticks = array_values(array_unique([0, intdiv($n - 1, 2), $n - 1]));
        $out = '';
        foreach ($ticks as $i) {
            $label = substr((string) ($series[$i]['date'] ?? ''), 5); // MM-DD
            $anchor = $i === 0 ? 'start' : ($i === $n - 1 ? 'end' : 'middle');
            $out .= sprintf('<text x="%.1f" y="%.1f" class="c-axis" text-anchor="%s">%s</text>', $x($i), $baseY + 16, $anchor, $this->esc($label));
        }

        return $out;
    }

    /**
     * @param list<array{date: string, views?: int, visitors?: int}> $series
     * @param list<array{key: string}> $lines
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

    /**
     * @param list<array{0: float, 1: float}> $points
     */
    private function points(array $points): string
    {
        return implode(' ', array_map(static fn (array $p): string => sprintf('%.1f,%.1f', $p[0], $p[1]), $points));
    }

    private function svg(string $body, string $ariaLabel): string
    {
        return sprintf(
            '<svg viewBox="0 0 %d %d" class="chart" role="img" aria-label="%s" preserveAspectRatio="xMidYMid meet">%s</svg>',
            self::W, self::H, $this->esc($ariaLabel), $body,
        );
    }

    private function empty(string $ariaLabel): string
    {
        return sprintf(
            '<svg viewBox="0 0 %d %d" class="chart" role="img" aria-label="%s"><text x="%d" y="%d" text-anchor="middle" class="c-empty">Aucune donnée</text></svg>',
            self::W, self::H, $this->esc($ariaLabel), self::W / 2, self::H / 2,
        );
    }

    private function num(float $v): string
    {
        return number_format($v, 0, '.', ' ');
    }

    private function esc(string $s): string
    {
        return htmlspecialchars($s, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
    }
}
