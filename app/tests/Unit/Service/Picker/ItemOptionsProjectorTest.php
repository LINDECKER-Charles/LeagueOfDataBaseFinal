<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Picker;

use App\Service\Picker\GameMode;
use App\Service\Picker\ItemOptionsProjector;
use PHPUnit\Framework\TestCase;

/**
 * Item options are shop-filtered (purchasable, playable on the requested
 * mode's map — Summoner's Rift by default —, not hidden, not champion-bound),
 * string-id'd despite PHP's int map keys, and `from` deduplicated. Resolution
 * stays presence-based (unfiltered).
 */
final class ItemOptionsProjectorTest extends TestCase
{
    private ItemOptionsProjector $projector;

    protected function setUp(): void
    {
        $this->projector = new ItemOptionsProjector();
    }

    /** @return array<int|string, array<string, mixed>> int keys on purpose — json_decode does the same */
    private function data(): array
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
            2010 => [
                'name' => 'Locked Biscuit',
                'gold' => ['total' => 50, 'purchasable' => false],
                'maps' => [11 => true],
            ],
            3070 => [
                'name' => 'Aram Only',
                'gold' => ['total' => 400, 'purchasable' => true],
                'maps' => [11 => false, 12 => true],
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

        self::assertSame(['3006', '1001'], array_column($options, 'id'), 'name order ("Berserker…" < "Boots"), ids restored to strings');
    }

    public function testProjectShapesTheContractFields(): void
    {
        $options = $this->projector->project($this->data(), ['Berserker Greaves' => 'cdn/blobs/3006.png']);
        $greaves = array_values(array_filter($options, static fn (array $o): bool => $o['id'] === '3006'))[0];

        self::assertSame([
            'id' => '3006',
            'name' => 'Berserker Greaves',
            'image' => '/cdn/blobs/3006.png',
            'gold' => 1100,
            'purchasable' => true,
            'tags' => ['Boots'],
            'from' => ['1001', '1042'],
            'into' => ['3172'],
            'depth' => 2,
        ], $greaves, '`from` deduplicated, `into` recast to strings');
    }

    public function testMissingImageAndDepthDegradeToNull(): void
    {
        $options = $this->projector->project($this->data(), []);
        $boots = array_values(array_filter($options, static fn (array $o): bool => $o['id'] === '1001'))[0];

        self::assertNull($boots['image']);
        self::assertNull($boots['depth']);
        self::assertSame([], $boots['from']);
    }

    public function testResolveIsPresenceBasedNotShopFiltered(): void
    {
        $resolved = $this->projector->resolve($this->data(), [], '2010');

        self::assertSame(['id' => '2010', 'name' => 'Locked Biscuit', 'image' => null, 'type' => 'item'], $resolved);
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

        self::assertSame(['Berserker Greaves'], $names, 'deduplicated; unknown ids are the validator\'s concern');
        self::assertSame([], $this->projector->unavailableOn($data, GameMode::SummonersRift, ['3006', '1001']));
    }
}
