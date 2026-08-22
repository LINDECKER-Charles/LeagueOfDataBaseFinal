<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Catalog\Facet;

use App\Service\API\RuneManager;
use App\Service\API\SummonerManager;
use App\Service\Catalog\Facet\Schema\RuneFacets;
use App\Service\Catalog\Facet\Schema\SummonerFacets;

/**
 * Summoner spells: modes from the curated list only (JADE stays an edition),
 * unlock levels read off the patch. Runes: positional facets from the tree and
 * slot the template attaches to each node.
 */
final class SummonerAndRuneFacetsTest extends FacetSchemaTestCase
{
    protected function setUp(): void
    {
        parent::setUp();
        $this->seed('summoner', ['data' => [
            'SummonerFlash' => ['id' => 'SummonerFlash', 'name' => 'Flash', 'summonerLevel' => 7, 'modes' => ['CLASSIC']],
            'SummonerSmite' => ['id' => 'SummonerSmite', 'name' => 'Smite', 'summonerLevel' => 9, 'modes' => ['CLASSIC']],
            'SummonerHeal'  => ['id' => 'SummonerHeal', 'name' => 'Heal', 'summonerLevel' => 1, 'modes' => ['CLASSIC']],
        ]]);
        $this->seed('runesReforged', [
            ['id' => 8100, 'key' => 'Domination', 'name' => 'Domination', 'slots' => []],
            ['id' => 8000, 'key' => 'Precision', 'name' => 'Precision', 'slots' => []],
        ]);
    }

    public function testSummonerValuesKeepCuratedModesAndFlagTheClassicEdition(): void
    {
        $facets = new SummonerFacets($this->manager(SummonerManager::class), $this->translator());

        $flash = $facets->valuesOf('SummonerFlash', [
            'modes' => ['CLASSIC', 'WIPMODEWIP', 'ARAM', 'TUTORIAL', 'RUBY_TRIAL_1'],
            'summonerLevel' => 7,
            'cooldown' => [300],
        ], $this->ref());
        $jade = $facets->valuesOf('SummonerFlash_Jade', [
            'modes' => ['JADE'], 'summonerLevel' => 1, 'cooldown' => [300],
        ], $this->ref());

        self::assertSame(['CLASSIC', 'ARAM'], $flash['mode']);
        self::assertSame('modern', $flash['edition']);
        self::assertSame('7', $flash['level']);
        self::assertSame(300.0, $flash['cooldown']);
        self::assertSame([], $jade['mode'], 'JADE is the edition axis, not a mode');
        self::assertSame('classic', $jade['edition']);
    }

    public function testSummonerSchemaReadsTheUnlockLevelsOffThePatch(): void
    {
        $facets = new SummonerFacets($this->manager(SummonerManager::class), $this->translator());
        $schema = self::byKey($facets->schema($this->ref()));

        self::assertSame(['1', '7', '9'], array_column($schema['level']->options, 'value'));
        self::assertSame('s', $schema['cooldown']->unit);
        self::assertArrayNotHasKey(
            'PRACTICETOOL',
            array_column($schema['mode']->options, 'label', 'value'),
        );
    }

    public function testRuneValuesArePositional(): void
    {
        $facets = new RuneFacets($this->manager(RuneManager::class), $this->translator());

        $keystone = $facets->valuesOf('Electrocute', ['key' => 'Electrocute', 'tree' => 'Domination', 'slot' => 0], $this->ref());
        $minor = $facets->valuesOf('CheapShot', ['key' => 'CheapShot', 'tree' => 'Domination', 'slot' => 1], $this->ref());
        $bare = $facets->valuesOf('CheapShot', ['key' => 'CheapShot'], $this->ref());

        self::assertSame(['path' => 'Domination', 'slot' => 'keystone'], $keystone);
        self::assertSame(['path' => 'Domination', 'slot' => 'row1'], $minor);
        self::assertSame([], $bare);
    }

    public function testRuneSchemaListsThePathsOfThePatchAndTheFourSlots(): void
    {
        $facets = new RuneFacets($this->manager(RuneManager::class), $this->translator());
        $schema = self::byKey($facets->schema($this->ref()));

        self::assertSame(['Domination', 'Precision'], array_column($schema['path']->options, 'value'));
        self::assertSame(['keystone', 'row1', 'row2', 'row3'], array_column($schema['slot']->options, 'value'));
        self::assertSame('facet.rune.slot_row{"%n%":2}', $schema['slot']->options[2]['label']);
    }
}
