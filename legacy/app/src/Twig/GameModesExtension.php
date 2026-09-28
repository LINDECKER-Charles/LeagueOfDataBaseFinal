<?php
declare(strict_types=1);

namespace App\Twig;

use App\Service\API\Edition\SummonerEditionRule;
use App\Service\Catalog\GameModeLabels;
use Symfony\Contracts\Translation\TranslatorInterface;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * Player-facing labels of Data Dragon's summoner-spell game modes on the
 * summoner cards and detail plaques — the curated list itself lives in
 * {@see GameModeLabels} (shared with the list facet); JADE, the League of
 * Legends Classic client, is an edition and reads as its translated label.
 */
final class GameModesExtension extends AbstractExtension
{
    public function __construct(private readonly TranslatorInterface $translator) {}

    public function getFunctions(): array
    {
        return [new TwigFunction('game_mode_labels', $this->labels(...))];
    }

    /**
     * Labels of the displayable modes, input order kept, duplicates and
     * internal ids dropped.
     *
     * @param iterable<mixed> $modes raw DDragon mode ids
     * @return list<string>
     */
    public function labels(iterable $modes): array
    {
        $labels = [];
        foreach ($modes as $mode) {
            $mode = (string) $mode;
            $label = $mode === SummonerEditionRule::CLASSIC_MODE
                ? $this->translator->trans('edition.classic')
                : GameModeLabels::labelOf($mode);
            if ($label !== null) {
                $labels[$mode] = $label;
            }
        }

        return array_values($labels);
    }
}
