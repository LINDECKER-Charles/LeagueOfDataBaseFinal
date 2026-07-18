<?php
declare(strict_types=1);

namespace App\Twig;

use App\Stat\GameStat;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * Exposes the {@see GameStat} catalogue to templates: resolves a stat's icon
 * and turns Data Dragon's raw item `stats` block into an ordered, labelled,
 * sign-formatted display list ready for the shared `.stat-board` markup.
 */
final class StatExtension extends AbstractExtension
{
    public function getFunctions(): array
    {
        return [
            new TwigFunction('stat_icon_url', $this->iconUrl(...)),
            new TwigFunction('item_stats', $this->itemStats(...)),
        ];
    }

    /** Icon path for a stat slug (e.g. 'armor'), or null when none is bundled. */
    public function iconUrl(string $slug): ?string
    {
        return GameStat::tryFrom($slug)?->icon();
    }

    /**
     * @param array<string,int|float>|null $stats Data Dragon item `stats` block
     *
     * @return list<array{slug: string, label: string, value: string}>
     */
    public function itemStats(?array $stats): array
    {
        return array_map(
            static fn (array $row): array => [
                'slug'  => $row['stat']->value,
                'label' => $row['stat']->labelKey(),
                'value' => self::format($row['value'], $row['percent']),
            ],
            GameStat::fromItemStats($stats),
        );
    }

    /** Signed display value: flats verbatim ("+75"), percents scaled ("+25 %"). */
    private static function format(float $value, bool $percent): string
    {
        $number = $percent ? round($value * 100, 1) : $value;
        $text = $number == (int) $number ? (string) (int) $number : (string) $number;

        return '+'.$text.($percent ? ' %' : '');
    }
}
