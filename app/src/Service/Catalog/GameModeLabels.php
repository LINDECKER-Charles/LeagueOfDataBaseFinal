<?php
declare(strict_types=1);

namespace App\Service\Catalog;

/**
 * The ONE curated list of Data Dragon summoner-spell game modes worth showing
 * to players, with Riot's product names as labels. DDragon mixes real queues
 * with internal dev ids (WIPMODEWIP, RUBY_TRIAL_1…, TUTORIAL_MODULE_1, KIWI…):
 * anything outside this map is deliberately never displayed nor filterable.
 *
 * CLASSIC is DDragon's id for the standard Summoner's Rift queue — not to be
 * confused with JADE, the League of Legends Classic client, which is an
 * edition ({@see \App\Service\API\Edition\SummonerEditionRule}) and is labelled
 * by the translator, not here.
 */
final class GameModeLabels
{
    private const LABELS = [
        'CLASSIC'      => "Summoner's Rift",
        'ARAM'         => 'ARAM',
        'CHERRY'       => 'Arena',
        'BRAWL'        => 'Brawl',
        'NEXUSBLITZ'   => 'Nexus Blitz',
        'URF'          => 'URF',
        'SNOWURF'      => 'Snow ARURF',
        'ARSR'         => 'ARSR',
        'ONEFORALL'    => 'One for All',
        'ULTBOOK'      => 'Ultimate Spellbook',
        'SWIFTPLAY'    => 'Swiftplay',
        'KINGPORO'     => 'Legend of the Poro King',
        'ASSASSINATE'  => 'Blood Moon',
        'PRACTICETOOL' => 'Practice Tool',
        'TUTORIAL'     => 'Tutorial',
    ];

    /**
     * Displayed on a spell's plaque, but not a queue a player filters spells
     * for — they would only add noise to the facet.
     */
    private const NOT_FACETABLE = ['PRACTICETOOL', 'TUTORIAL'];

    /** Product name of a displayable mode, null for an internal id. */
    public static function labelOf(string $mode): ?string
    {
        return self::LABELS[$mode] ?? null;
    }

    /**
     * The modes the list facet offers, in display order.
     *
     * @return array<string,string> mode id => label
     */
    public static function facetable(): array
    {
        return array_diff_key(self::LABELS, array_flip(self::NOT_FACETABLE));
    }
}
