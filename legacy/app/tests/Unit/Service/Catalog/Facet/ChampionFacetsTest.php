<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Catalog\Facet;

use App\Service\API\ChampionManager;
use App\Service\Catalog\Facet\FacetKind;
use App\Service\Catalog\Facet\Schema\ChampionFacets;

/**
 * The champion normalisations a shared link depends on: a locale-independent
 * resource token, Riot's melee/ranged split, and the raw ratings and stats.
 */
final class ChampionFacetsTest extends FacetSchemaTestCase
{
    private const EN = [
        'Ahri'    => ['partype' => 'Mana', 'attackrange' => 550],
        'Akali'   => ['partype' => 'Energy', 'attackrange' => 125],
        'Belveth' => ['partype' => '', 'attackrange' => 175],
        'Garen'   => ['partype' => 'None', 'attackrange' => 175],
        'Aatrox'  => ['partype' => 'Blood Well', 'attackrange' => 175],
        'Nilah'   => ['partype' => 'Mana', 'attackrange' => 225],
        'Lillia'  => ['partype' => 'Mana', 'attackrange' => 325],
        'Urgot'   => ['partype' => 'Mana', 'attackrange' => 350],
    ];
    private const FR = [
        'Ahri' => 'Mana', 'Akali' => 'Énergie', 'Belveth' => '', 'Garen' => 'Aucune',
        'Aatrox' => 'Puits de sang', 'Nilah' => 'Mana', 'Lillia' => 'Mana', 'Urgot' => 'Mana',
    ];

    protected function setUp(): void
    {
        parent::setUp();
        $this->seed('champion', ['data' => $this->entries(self::EN)]);
        $this->seed('champion', ['data' => $this->entries(self::EN, self::FR)], 'fr_FR');
    }

    public function testResourceTokenIsTheSameInEveryLocale(): void
    {
        $facets = $this->facets();
        $en = $this->manager(ChampionManager::class)->getData(self::VERSION, 'en_US')['data'];
        $fr = $this->manager(ChampionManager::class)->getData(self::VERSION, 'fr_FR')['data'];

        foreach (['Akali' => 'energy', 'Belveth' => 'none', 'Garen' => 'none', 'Aatrox' => 'blood-well'] as $id => $token) {
            self::assertSame($token, $facets->valuesOf($id, $en[$id], $this->ref())['resource']);
            self::assertSame($token, $facets->valuesOf($id, $fr[$id], $this->ref('fr_FR'))['resource']);
        }
    }

    public function testResourceOptionsAreLabelledFromTheReadersLocaleMostCommonFirst(): void
    {
        $options = self::byKey($this->facets()->schema($this->ref('fr_FR')))['resource']->options;

        self::assertSame(['mana', 'none', 'energy', 'blood-well'], array_column($options, 'value'));
        self::assertSame('Énergie', $options[2]['label']);
        self::assertSame('facet.champion.resource_none', $options[1]['label']);
    }

    public function testRangeBucketsFollowRiotsOwnSplit(): void
    {
        $facets = $this->facets();
        $data = $this->manager(ChampionManager::class)->getData(self::VERSION, 'en_US')['data'];

        foreach (['Akali' => 'melee', 'Nilah' => 'melee', 'Lillia' => 'melee', 'Urgot' => 'ranged', 'Ahri' => 'ranged'] as $id => $bucket) {
            self::assertSame($bucket, $facets->valuesOf($id, $data[$id], $this->ref())['range'], $id);
        }
    }

    public function testCarriesRolesRatingsAndLevelOneStats(): void
    {
        $values = $this->facets()->valuesOf('Ahri', [
            'tags' => ['Mage', 'Assassin'],
            'info' => ['attack' => 3, 'defense' => 4, 'magic' => 8, 'difficulty' => 5],
            'stats' => [
                'hp' => 590, 'armor' => 21, 'spellblock' => 30, 'attackdamage' => 53,
                'attackspeed' => 0.668, 'movespeed' => 330, 'attackrange' => 550, 'crit' => 0,
            ],
        ], $this->ref());

        self::assertSame(['Mage', 'Assassin'], $values['role']);
        self::assertSame(['difficulty' => 5, 'attack' => 3, 'defense' => 4, 'magic' => 8], array_intersect_key(
            $values,
            array_flip(['attack', 'defense', 'magic', 'difficulty']),
        ));
        self::assertSame(0.668, $values['as']);
        self::assertSame(330.0, $values['ms']);
        self::assertArrayNotHasKey('crit', $values, 'dead list-level stats are not facets');
    }

    public function testSchemaShapesTheProfileChipsAndTheRanges(): void
    {
        $schema = self::byKey($this->facets()->schema($this->ref()));

        self::assertSame(
            ['role', 'resource', 'range', 'difficulty', 'attack', 'defense', 'magic', 'hp', 'armor', 'mr', 'ad', 'as', 'ms'],
            array_keys($schema),
        );
        self::assertTrue($schema['role']->isPrimary);
        self::assertSame(FacetKind::Range, $schema['hp']->kind);
        self::assertSame(0.01, $schema['as']->step);
        self::assertSame('stat.health', $schema['hp']->label);
        self::assertSame(
            ['Fighter', 'Tank', 'Mage', 'Assassin', 'Marksman', 'Support'],
            array_column($schema['role']->options, 'value'),
        );
    }

    private function facets(): ChampionFacets
    {
        return new ChampionFacets($this->manager(ChampionManager::class), $this->translator());
    }

    /**
     * @param array<string, array{partype: string, attackrange: int}> $base
     * @param array<string, string> $partypes localized overrides
     * @return array<string, array<string, mixed>>
     */
    private function entries(array $base, array $partypes = []): array
    {
        $entries = [];
        foreach ($base as $id => $entry) {
            $entries[$id] = [
                'id' => $id,
                'name' => $id,
                'partype' => $partypes[$id] ?? $entry['partype'],
                'tags' => ['Mage'],
                'stats' => ['attackrange' => $entry['attackrange']],
            ];
        }

        return $entries;
    }
}
