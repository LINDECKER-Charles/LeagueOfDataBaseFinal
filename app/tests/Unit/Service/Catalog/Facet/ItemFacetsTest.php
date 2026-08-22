<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Catalog\Facet;

use App\Service\API\ItemManager;
use App\Service\Catalog\Facet\Schema\ItemFacets;

/**
 * The item normalisations: structural tiers, maps without the Classic flag,
 * percent stats exposed as percents, and no stat value for an empty block.
 */
final class ItemFacetsTest extends FacetSchemaTestCase
{
    protected function setUp(): void
    {
        parent::setUp();
        $this->seed('item', ['data' => [
            '1001' => ['name' => 'Boots', 'tags' => ['Boots'], 'maps' => []],
            '3020' => ['name' => 'Sorcerer\'s Shoes', 'tags' => ['Boots', 'MagicPenetration'], 'maps' => []],
        ]]);
    }

    public function testDerivesEveryValueOfALegendary(): void
    {
        $values = $this->facets()->valuesOf('3078', [
            'name' => 'Trinity Force',
            'tags' => ['Health', 'Damage', 'AttackSpeed'],
            'maps' => ['11' => true, '12' => true, '21' => false, '22' => false, '30' => false, '453' => true],
            'depth' => 3,
            'gold' => ['total' => 3333, 'purchasable' => true],
            'stats' => ['FlatHPPoolMod' => 333, 'FlatPhysicalDamageMod' => 36, 'PercentAttackSpeedMod' => 0.3],
        ], $this->ref());

        self::assertSame(['Health', 'Damage', 'AttackSpeed'], $values['tag']);
        self::assertSame('modern', $values['edition']);
        self::assertSame(['11', '12'], $values['map'], '453 is an edition, never a map');
        self::assertSame('legendary', $values['tier']);
        self::assertTrue($values['purchasable']);
        self::assertArrayNotHasKey('consumable', $values);
        self::assertSame(3333, $values['price']);
        self::assertSame(333.0, $values['health']);
        self::assertSame(36.0, $values['attack_damage']);
        self::assertSame(30, $values['attack_speed_pct']);
    }

    public function testTierIsStructural(): void
    {
        $facets = $this->facets();
        $tier = fn (array $entry): ?string => $facets->valuesOf('1', $entry, $this->ref())['tier'] ?? null;

        self::assertSame('component', $tier(['into' => ['3006']]));
        self::assertSame('epic', $tier(['depth' => 2, 'into' => ['3078']]));
        self::assertSame('legendary', $tier(['depth' => 4]));
        self::assertNull($tier(['consumed' => true]), 'a consumable is no tier');
        self::assertNull($tier(['gold' => ['purchasable' => false]]), 'a quest reward is no tier');
    }

    public function testAnEmptyStatsBlockYieldsNoStatValue(): void
    {
        $values = $this->facets()->valuesOf('2003', [
            'name' => 'Health Potion', 'consumed' => true, 'gold' => ['total' => 50, 'purchasable' => true], 'stats' => [],
        ], $this->ref());

        self::assertTrue($values['consumable']);
        foreach (array_keys($values) as $key) {
            self::assertStringNotContainsString('_pct', $key);
        }
        self::assertArrayNotHasKey('health', $values);
    }

    public function testClassicTwinsAreFlaggedByIdAndFlatMoveSpeedIsItsOwnFacet(): void
    {
        $values = $this->facets()->valuesOf('771001', [
            'name' => 'Boots of Speed', 'stats' => ['FlatMovementSpeedMod' => 25, 'PercentMovementSpeedMod' => 0.05],
        ], $this->ref());

        self::assertSame('classic', $values['edition']);
        self::assertSame(25.0, $values['move_speed']);
        self::assertSame(5, $values['move_speed_pct']);
    }

    public function testSchemaOffersThePatchTagsHumanisedAndTheStatColumns(): void
    {
        $schema = self::byKey($this->facets()->schema($this->ref()));

        self::assertSame(
            [['value' => 'Boots', 'label' => 'Boots'], ['value' => 'MagicPenetration', 'label' => 'Magic Penetration']],
            $schema['tag']->options,
        );
        self::assertTrue($schema['tag']->canMatchAll);
        self::assertFalse($schema['edition']->isMultiple);
        self::assertSame(['11', '12', '21', '30', '33', '35'], array_column($schema['map']->options, 'value'));
        self::assertSame('%', $schema['attack_speed_pct']->unit);
        self::assertNull($schema['health']->unit);
        self::assertSame('stat.move_speed', $schema['move_speed_pct']->label);
    }

    private function facets(): ItemFacets
    {
        return new ItemFacets($this->manager(ItemManager::class), $this->translator());
    }
}
