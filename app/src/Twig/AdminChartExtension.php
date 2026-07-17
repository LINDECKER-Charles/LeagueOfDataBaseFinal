<?php
declare(strict_types=1);

namespace App\Twig;

use App\Service\Analytics\Chart\SvgChartRenderer;
use Twig\Extension\AbstractExtension;
use Twig\TwigFilter;
use Twig\TwigFunction;

/**
 * Thin Twig adapter over {@see SvgChartRenderer}: chart functions return ready
 * SVG (marked html-safe so templates need no |raw) and the number formatters are
 * surfaced as filters. All rendering logic stays in the pure renderer.
 */
final class AdminChartExtension extends AbstractExtension
{
    public function __construct(private readonly SvgChartRenderer $charts) {}

    public function getFunctions(): array
    {
        $html = ['is_safe' => ['html']];

        return [
            new TwigFunction('chart_timeseries', $this->charts->timeSeries(...), $html),
            new TwigFunction('chart_donut', $this->charts->donut(...), $html),
            new TwigFunction('chart_sparkline', $this->charts->sparkline(...), $html),
            new TwigFunction('heat_color', $this->charts->heatColor(...)),
        ];
    }

    public function getFilters(): array
    {
        return [
            new TwigFilter('bytes', $this->charts->bytes(...)),
            new TwigFilter('compact', $this->charts->compact(...)),
            new TwigFilter('pct', $this->percent(...)),
            new TwigFilter('euros', $this->euros(...)),
        ];
    }

    public function percent(int|float $value, int $decimals = 1): string
    {
        return number_format((float) $value, $decimals) . ' %';
    }

    /** Cent-stored amounts (donations, plans) rendered as French-format euros. */
    public function euros(int|float $cents): string
    {
        return number_format($cents / 100, 2, ',', ' ') . ' €';
    }
}
