<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\Build\BuildStructureProjector;
use App\Service\Picker\GameMode;
use App\Service\Picker\ItemOptionsProjector;
use PHPUnit\Framework\TestCase;

/**
 * Offline coverage of the cross-version import projection: keep the components
 * that exist (and are usable) on the target patch, drop the rest, report what
 * went. The item projector is the real (pure) one; catalogs are fixtures.
 */
final class BuildStructureProjectorTest extends TestCase
{
    private BuildStructureProjector $projector;

    protected function setUp(): void
    {
        $this->projector = new BuildStructureProjector(new ItemOptionsProjector());
    }

    /** @return array{runeTrees: array<mixed>, championIds: list<string>, itemData: array<int, array<string, mixed>>} */
    private static function catalogs(): array
    {
        $tree = static fn (int $id, array $slots): array => [
            'id' => $id,
            'slots' => array_map(
                static fn (array $perkIds): array => ['runes' => array_map(static fn (int $p): array => ['id' => $p], $perkIds)],
                $slots,
            ),
        ];

        return [
            'runeTrees' => [
                $tree(8000, [[8005, 8008], [9101, 9111], [9104, 9105], [8014, 8017]]),
                $tree(8100, [[8112, 8124], [8126, 8139], [8138, 8135], [8106, 8105]]),
            ],
            'championIds' => ['Aatrox', 'Ahri'],
            'itemData' => [
                1055 => ['name' => "Doran's Blade", 'maps' => [11 => true, 12 => true]],
                3006 => ['name' => 'Berserker Greaves', 'maps' => [11 => true, 12 => false]],
                2003 => ['name' => 'Health Potion'],
            ],
        ];
    }

    /** @return array<mixed> */
    private static function validRunes(): array
    {
        return [
            'primaryStyleId' => 8000,
            'primarySelections' => [8005, 9101, 9104, 8014],
            'secondaryStyleId' => 8100,
            'secondarySelections' => [8126, 8138],
        ];
    }

    public function testCompatibleBuildCarriesOverUntouched(): void
    {
        $structure = [
            'championId' => 'Aatrox',
            'runes' => self::validRunes(),
            'steps' => [['label' => 'Start', 'note' => 'go', 'items' => ['1055', '2003']]],
        ];

        $result = $this->projector->project($structure, GameMode::SummonersRift, self::catalogs());

        self::assertFalse($result['report']['championMissing']);
        self::assertFalse($result['report']['runesReset']);
        self::assertSame([], $result['report']['droppedItems']);
        self::assertSame(self::validRunes(), $result['structure']['runes']);
        self::assertSame(['1055', '2003'], $result['structure']['steps'][0]['items']);
    }

    public function testMissingChampionIsFlaggedButTheIdIsKept(): void
    {
        $structure = ['championId' => 'Nobody', 'runes' => self::validRunes(), 'steps' => []];

        $result = $this->projector->project($structure, GameMode::SummonersRift, self::catalogs());

        self::assertTrue($result['report']['championMissing']);
        self::assertSame('Nobody', $result['structure']['championId']);
    }

    public function testItemsAbsentOrOffTheModeMapAreDroppedAndEmptiedStepsRemoved(): void
    {
        $structure = [
            'championId' => 'Aatrox',
            'runes' => self::validRunes(),
            'steps' => [
                ['label' => 'Core', 'note' => null, 'items' => ['1055', '3006', '9999']],
                ['label' => 'Boots only', 'note' => null, 'items' => ['3006']],
            ],
        ];

        // Arena/ARAM map 12: Greaves (3006) is off-map, 9999 is unknown → both drop.
        $result = $this->projector->project($structure, GameMode::Aram, self::catalogs());

        self::assertSame(['1055'], $result['structure']['steps'][0]['items']);
        self::assertCount(1, $result['structure']['steps'], 'the step left with no item is removed');

        $byId = array_column($result['report']['droppedItems'], 'name', 'id');
        self::assertSame('Berserker Greaves', $byId['3006'], 'known item drops with its name');
        self::assertSame('9999', $byId['9999'], 'unknown id falls back to the id');
    }

    public function testRunesAreResetWhenAnIdIsGoneFromTheTargetTrees(): void
    {
        $structure = [
            'championId' => 'Aatrox',
            'runes' => [
                'primaryStyleId' => 8000,
                'primarySelections' => [8005, 9101, 9104, 7777], // 7777 exists on no target tree
                'secondaryStyleId' => 8100,
                'secondarySelections' => [8126, 8138],
            ],
            'steps' => [['label' => 'Start', 'note' => null, 'items' => ['1055']]],
        ];

        $result = $this->projector->project($structure, GameMode::SummonersRift, self::catalogs());

        self::assertTrue($result['report']['runesReset']);
        self::assertSame(
            ['primaryStyleId' => 0, 'primarySelections' => [], 'secondaryStyleId' => 0, 'secondarySelections' => []],
            $result['structure']['runes'],
        );
    }
}
