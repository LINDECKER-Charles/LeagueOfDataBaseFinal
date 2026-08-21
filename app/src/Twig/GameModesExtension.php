<?php
declare(strict_types=1);

namespace App\Twig;

use App\Service\API\Edition\SummonerEditionRule;
use Symfony\Contracts\Translation\TranslatorInterface;
use Twig\Extension\AbstractExtension;
use Twig\TwigFunction;

/**
 * Player-facing labels of Data Dragon's summoner-spell game modes — the ONE
 * curated list shared by the summoner list cards and the detail plaques, which
 * used to keep two diverging copies. DDragon mixes real queues with internal
 * dev ids (WIPMODEWIP, RUBY_TRIAL_1…, TUTORIAL_MODULE_1, KIWI…): anything
 * outside this map is deliberately not displayed.
 */
final class GameModesExtension extends AbstractExtension
{
    /**
     * Riot's persistent, named queues, product names as labels. CLASSIC is
     * DDragon's id for the standard Summoner's Rift queue — not to be confused
     * with JADE, the League of Legends Classic client (translated label).
     */
    private const MODE_LABELS = [
        'CLASSIC'      => "Summoner's Rift",
        'ARAM'         => 'ARAM',
        'CHERRY'       => 'Arena',
        'BRAWL'        => 'Brawl',
        'NEXUSBLITZ'   => 'Nexus Blitz',
        'URF'          => 'URF',
        'SNOWURF'      => 'Snow ARURF',
        'ONEFORALL'    => 'One for All',
        'ULTBOOK'      => 'Ultimate Spellbook',
        'SWIFTPLAY'    => 'Swiftplay',
        'KINGPORO'     => 'Legend of the Poro King',
        'ASSASSINATE'  => 'Blood Moon',
        'PRACTICETOOL' => 'Practice Tool',
        'TUTORIAL'     => 'Tutorial',
    ];

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
                : (self::MODE_LABELS[$mode] ?? null);
            if ($label !== null) {
                $labels[$mode] = $label;
            }
        }

        return array_values($labels);
    }
}
