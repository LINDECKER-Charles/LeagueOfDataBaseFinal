<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Picker;

use App\Service\Picker\GameMode;
use App\Service\Picker\ItemOptionsProjector;
use PHPUnit\Framework\TestCase;

/**
 * Item options are shop-filtered (purchasable, playable on the requested
 * mode's map — Summoner's Rift by default —, not hidden, not champion-bound)
 * and string-id'd despite PHP's int map keys. Resolution stays presence-based
 * (unfiltered).
 */
final class ItemOptionsProjectorTest extends TestCase
{
    private ItemOptionsProjector $projector;

    protected function setUp(): void
    {
        $this->projector = new ItemOptionsProjector();
    }

    /**
     * Int keys on purpose — json_decode does the same.
     *
     * @return array<int|string, array<string, mixed>>
     */
    private function data(): array
    {
        return $this->riftShopItems() + $this->mapRestrictedItems() + $this->unpickableItems();
    }

    /** Purchasable on Summoner's Rift — the only two the default projection keeps. */
    private function riftShopItems(): array
    {
        return [
            3006 => [
                'name' => 'Berserker Greaves',
                'image' => ['full' => '3006.png'],
                'gold' => ['total' => 1100, 'purchasable' => true],
                'tags' => ['Boots'],
                'from' => ['1001', '1001', '1042'],
                'into' => [3172],
                'maps' => [11 => true],
                'depth' => 2,
            ],
            1001 => [
                'name' => 'Boots',
                'image' => ['full' => '1001.png'],
                'gold' => ['total' => 300, 'purchasable' => true],
                'maps' => [11 => true],
            ],
        ];
    }

    /** Playable on the Howling Abyss (map 12) only. */
    private function mapRestrictedItems(): array
    {
        return [
            3070 => [
                'name' => 'Aram Only',
                'gold' => ['total' => 400, 'purchasable' => true],
                'maps' => [11 => false, 12 => true],
            ],
        ];
    }

    /** Excluded from the shop whatever the mode: not for sale, hidden, champion-bound. */
    private function unpickableItems(): array
    {
        return [
            2010 => [
                'name' => 'Locked Biscuit',
                'gold' => ['total' => 50, 'purchasable' => false],
                'maps' => [11 => true],
            ],
            7013 => [
                'name' => 'Hidden Ornn Thing',
                'gold' => ['total' => 0, 'purchasable' => true],
                'hideFromAll' => true,
            ],
            3599 => [
                'name' => 'Kalista Spear',
                'gold' => ['total' => 0, 'purchasable' => true],
                'requiredChampion' => 'Kalista',
            ],
        ];
    }

    public function testProjectFiltersToShopPickableItems(): void
    {
        $options = $this->projector->project($this->data(), []);

        self::assertSame(
            ['3006', '1001'],
            array_column($options, 'id'),
            'name order ("Berserker…" < "Boots"), ids restored to strings',
        );
    }

    public function testProjectShapesTheContractFields(): void
    {
        $options = $this->projector->project(
            $this->data(),
            ['3006' => 'cdn/blobs/3006.png'],
        );
        $greaves = array_values(array_filter(
            $options,
            static fn (array $o): bool => $o['id'] === '3006',
        ))[0];

        self::assertSame([
            'id' => '3006',
            'name' => 'Berserker Greaves',
            'image' => '/cdn/blobs/3006.png',
            'gold' => 1100,
            'tags' => ['Boots'],
        ], $greaves, 'exactly the five fields the picker island consumes');
    }

    public function testMissingImageDegradesToNull(): void
    {
        $options = $this->projector->project($this->data(), []);
        $boots = array_values(array_filter(
            $options,
            static fn (array $o): bool => $o['id'] === '1001',
        ))[0];

        self::assertNull($boots['image']);
        self::assertSame([], $boots['tags'], 'a tag-less entry still carries the key');
    }

    public function testResolveIsPresenceBasedNotShopFiltered(): void
    {
        $resolved = $this->projector->resolve($this->data(), [], '2010');

        self::assertSame(
            ['id' => '2010', 'name' => 'Locked Biscuit', 'image' => null, 'type' => 'item'],
            $resolved,
        );
        self::assertNull($this->projector->resolve($this->data(), [], '9999'));
    }

    public function testProjectionFollowsTheRequestedMode(): void
    {
        $data = $this->data();
        // Berserker Greaves is explicitly banned from the Howling Abyss.
        $data[3006]['maps'] = [11 => true, 12 => false];

        $aram = array_column($this->projector->project($data, [], GameMode::Aram), 'id');

        self::assertContains('3070', $aram, 'map-12 item becomes pickable in ARAM');
        self::assertNotContains('3006', $aram, 'map-12=false item is excluded in ARAM');
        // A missing maps flag never excludes (older item.json predate some maps).
        self::assertContains('1001', $aram, 'no maps["12"] flag counts as available');
    }

    public function testUnavailableOnListsHonestNamesForTheModeErrors(): void
    {
        $data = $this->data();
        $data[3006]['maps'] = [11 => true, 12 => false];

        $names = $this->projector->unavailableOn(
            $data,
            GameMode::Aram,
            ['3006', '3006', '1001', '9999'], // duplicate + available + unknown-to-dataset
        );

        self::assertSame(
            ['Berserker Greaves'],
            $names,
            'deduplicated; unknown ids are the validator\'s concern',
        );
        self::assertSame(
            [],
            $this->projector->unavailableOn($data, GameMode::SummonersRift, ['3006', '1001']),
        );
    }

    /**
     * The LoL Classic catalogue (77xxxx) is never offered: every build mode is a
     * current-game queue, whatever item.json's maps flags claim (the classic
     * twin of Boots is flagged on map 12, and this one even on map 11).
     */
    public function testClassicItemsAreExcludedFromEveryModeDespiteTheirMapFlags(): void
    {
        $data = $this->data() + [
            771001 => [
                'name' => 'Boots',
                'image' => ['full' => '771001.png'],
                'gold' => ['total' => 325, 'purchasable' => true],
                'maps' => [11 => true, 12 => true, 453 => true],
            ],
        ];

        foreach (GameMode::cases() as $mode) {
            self::assertNotContains(
                '771001',
                array_column($this->projector->project($data, [], $mode), 'id'),
                $mode->value,
            );
            self::assertFalse($this->projector->isPlayable($data, $mode, '771001'), $mode->value);
        }
        self::assertContains('1001', array_column($this->projector->project($data, []), 'id'));
    }

    /** A classic twin in the error list is id-qualified: its bare name is its namesake's. */
    public function testUnavailableOnQualifiesAClassicTwinByItsId(): void
    {
        $data = $this->data() + [
            771001 => [
                'name' => 'Boots',
                'gold' => ['total' => 325, 'purchasable' => true],
                'maps' => [12 => true, 453 => true],
            ],
        ];

        self::assertSame(
            ['Boots [771001]'],
            $this->projector->unavailableOn($data, GameMode::Aram, ['1001', '771001']),
        );
    }

    /** Icons are looked up by id: the classic twin never borrows its namesake's. */
    public function testResolveReadsTheIconOfTheExactId(): void
    {
        $data = $this->data() + [
            771001 => ['name' => 'Boots', 'gold' => ['total' => 325, 'purchasable' => true]],
        ];
        $images = ['1001' => 'cdn/blobs/boots.png', '771001' => 'cdn/blobs/classic-boots.png'];

        self::assertSame(
            '/cdn/blobs/classic-boots.png',
            $this->projector->resolve($data, $images, '771001')['image'],
        );
        self::assertSame(
            '/cdn/blobs/boots.png',
            $this->projector->resolve($data, $images, '1001')['image'],
        );
    }
}
