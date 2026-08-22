<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Catalog;

use App\Service\Catalog\GameModeLabels;
use PHPUnit\Framework\TestCase;

/**
 * The curated mode list the summoner cards, plaques and list facet all read:
 * internal dev ids never surface, and the facet drops the queues nobody picks
 * spells for (practice tool, tutorial).
 */
final class GameModeLabelsTest extends TestCase
{
    public function testLabelsRiotsNamedQueuesAndNothingElse(): void
    {
        self::assertSame("Summoner's Rift", GameModeLabels::labelOf('CLASSIC'));
        self::assertSame('Arena', GameModeLabels::labelOf('CHERRY'));
        self::assertSame('ARSR', GameModeLabels::labelOf('ARSR'));
        self::assertNull(GameModeLabels::labelOf('WIPMODEWIP'));
        self::assertNull(GameModeLabels::labelOf('RUBY_TRIAL_1'));
        self::assertNull(GameModeLabels::labelOf('JADE'), 'LoL Classic is an edition, not a mode');
    }

    public function testTheFacetOffersTheDisplayableQueuesMinusTheNonGames(): void
    {
        $facetable = GameModeLabels::facetable();

        self::assertArrayHasKey('CLASSIC', $facetable);
        self::assertArrayHasKey('ARAM', $facetable);
        self::assertArrayNotHasKey('PRACTICETOOL', $facetable);
        self::assertArrayNotHasKey('TUTORIAL', $facetable);
        foreach ($facetable as $mode => $label) {
            self::assertSame($label, GameModeLabels::labelOf($mode));
        }
    }
}
