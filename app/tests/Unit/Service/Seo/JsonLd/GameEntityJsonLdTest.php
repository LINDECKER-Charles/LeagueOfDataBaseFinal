<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Seo\JsonLd;

use App\Service\Seo\JsonLd\GameEntityJsonLd;
use App\Service\Seo\JsonLd\JsonLdEncoder;
use PHPUnit\Framework\TestCase;

final class GameEntityJsonLdTest extends TestCase
{
    private const URL = 'https://example.com/champion/Aatrox';

    private GameEntityJsonLd $entities;

    protected function setUp(): void
    {
        $this->entities = new GameEntityJsonLd(new JsonLdEncoder());
    }

    public function testChampionProjectsRolesResourceRatingsAndBaseStats(): void
    {
        $graph = $this->entities->champion([
            'name'    => 'Aatrox',
            'title'   => 'the Darkin Blade',
            'blurb'   => 'Once honored defenders of Shurima…',
            'tags'    => ['Fighter', 'Tank'],
            'partype' => 'Blood Well',
            'info'    => ['attack' => 8, 'defense' => 4, 'magic' => 3, 'difficulty' => 4],
            'stats'   => ['hp' => 650, 'armor' => 38, 'attackdamage' => 60, 'hpperlevel' => 114],
        ], 'https://cdn/aatrox.png', self::URL);

        self::assertSame('VideoGame', $graph['@type']);
        self::assertSame(GameEntityJsonLd::GAME_NAME, $graph['name']);
        self::assertSame(self::URL . '#game', $graph['@id']);

        $character = $graph['character'];
        self::assertSame('Person', $character['@type']);
        self::assertSame('the Darkin Blade', $character['alternateName']);

        $properties = $this->propertyMap($character['additionalProperty']);
        self::assertSame(['Fighter', 'Tank'], $properties['Role']);
        self::assertSame(['Blood Well'], $properties['Resource']);
        self::assertSame([8], $properties['Attack rating']);
        self::assertSame([650], $properties['Health']);
        self::assertSame([60], $properties['Attack damage']);
        // Growth curves are deliberately excluded — they do not read as a fact.
        self::assertArrayNotHasKey('hpperlevel', $properties);
    }

    public function testItemStatesItsGoldEconomyWithAnExplicitUnit(): void
    {
        $graph = $this->entities->item([
            'name'      => 'Infinity Edge',
            'plaintext' => 'Massively increases critical strike damage',
            'gold'      => ['base' => 725, 'total' => 3300, 'sell' => 2310, 'purchasable' => true],
            'tags'      => ['CriticalStrike', 'Damage'],
        ], null, 'https://example.com/object/3031');

        $entity = $graph['gameItem'];
        self::assertSame('Thing', $entity['@type']);

        $properties = $entity['additionalProperty'];
        $total = array_values(array_filter(
            $properties,
            static fn (array $p): bool => $p['name'] === 'Total cost',
        ))[0];
        self::assertSame(3300, $total['value']);
        self::assertSame('gold', $total['unitText']);

        $map = $this->propertyMap($properties);
        self::assertSame(['CriticalStrike', 'Damage'], $map['Category']);
        // Purchasable items say nothing; only the exception is worth stating.
        self::assertArrayNotHasKey('Purchasable', $map);
    }

    public function testUnpurchasableItemSaysSoExplicitly(): void
    {
        $graph = $this->entities->item([
            'name' => 'Ornn masterwork',
            'gold' => ['total' => 0, 'purchasable' => false],
        ], null, 'https://example.com/object/7000');

        $map = $this->propertyMap($graph['gameItem']['additionalProperty']);
        self::assertSame(['false'], $map['Purchasable']);
    }

    public function testSummonerSpellUnwrapsPerRankArrays(): void
    {
        $graph = $this->entities->summonerSpell([
            'name'          => 'Flash',
            'description'   => 'Teleports your champion <b>a short distance</b>.',
            'cooldown'      => [300],
            'range'         => [400],
            'summonerLevel' => 7,
        ], null, 'https://example.com/summoner/SummonerFlash');

        $entity = $graph['gameItem'];
        // Markup from Data Dragon must not leak into structured data.
        self::assertSame('Teleports your champion a short distance.', $entity['description']);

        $map = $this->propertyMap($entity['additionalProperty']);
        self::assertSame([300], $map['Cooldown']);
        self::assertSame([400], $map['Range']);
        self::assertSame([7], $map['Required level']);
    }

    public function testRunePathCountsItsSlots(): void
    {
        $graph = $this->entities->runePath([
            'name'  => 'Precision',
            'key'   => 'Precision',
            'slots' => [['runes' => []], ['runes' => []], ['runes' => []], ['runes' => []]],
        ], 'https://cdn/precision.png', 'https://example.com/rune/Precision');

        $map = $this->propertyMap($graph['gameItem']['additionalProperty']);
        self::assertSame([4], $map['Slots']);
    }

    public function testMissingFieldsAreOmittedRatherThanEmittedEmpty(): void
    {
        // A patch that drops a key must degrade the node, never break the page.
        $graph = $this->entities->champion(['name' => 'Mystery'], null, self::URL);

        $character = $graph['character'];
        self::assertSame(['@type', '@id', 'name'], array_keys($character));
        self::assertArrayNotHasKey('additionalProperty', $character);
    }

    /**
     * @param list<array<string,mixed>> $properties
     * @return array<string, list<mixed>> property name => every value carried under it
     */
    private function propertyMap(array $properties): array
    {
        $map = [];
        foreach ($properties as $property) {
            $map[$property['name']][] = $property['value'];
        }

        return $map;
    }
}
