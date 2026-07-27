<?php
declare(strict_types=1);

namespace App\Service\Analytics\Chart;

/**
 * Shared SVG building blocks for the admin charts: the fixed canvas box, the
 * polyline/area geometry and the escaping helper. Pure and stateless — every
 * chart composes it instead of re-deriving the same markup.
 *
 * Charts scale to their container width through the viewBox (`width:100%;
 * height:auto` in admin.css), preserving aspect ratio. Strokes carry
 * `vector-effect="non-scaling-stroke"` so a client-side zoom transform stretches
 * the geometry without thickening the lines.
 */
final class SvgPrimitives
{
    public const W = 760;
    public const H = 240;
    public const PAD_X = 34;
    public const PAD_TOP = 16;
    public const PAD_BOTTOM = 26;

    /** @param list<array{0: float, 1: float}> $points */
    public function polyline(array $points, string $color, float $width = 2): string
    {
        return sprintf(
            '<polyline fill="none" stroke="%s" stroke-width="%s" stroke-linejoin="round" stroke-linecap="round"'
            . ' vector-effect="non-scaling-stroke" points="%s"/>',
            $color, $width, $this->points($points),
        );
    }

    /** @param list<array{0: float, 1: float}> $points */
    public function area(array $points, float $baseline, string $color, float $opacity = 0.12): string
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

    /** @param list<array{0: float, 1: float}> $points */
    public function points(array $points): string
    {
        return implode(' ', array_map(static fn (array $p): string => sprintf('%.1f,%.1f', $p[0], $p[1]), $points));
    }

    public function svg(string $body, string $ariaLabel): string
    {
        return sprintf(
            '<svg viewBox="0 0 %d %d" class="chart" role="img" aria-label="%s" preserveAspectRatio="xMidYMid meet">%s</svg>',
            self::W, self::H, $this->esc($ariaLabel), $body,
        );
    }

    public function empty(string $ariaLabel): string
    {
        return sprintf(
            '<svg viewBox="0 0 %d %d" class="chart" role="img" aria-label="%s"><text x="%d" y="%d" text-anchor="middle" class="c-empty">Aucune donnée</text></svg>',
            self::W, self::H, $this->esc($ariaLabel), self::W / 2, self::H / 2,
        );
    }

    public function esc(string $s): string
    {
        return htmlspecialchars($s, ENT_QUOTES | ENT_SUBSTITUTE, 'UTF-8');
    }
}
