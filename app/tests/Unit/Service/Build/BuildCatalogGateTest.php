<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\API\ChampionManager;
use App\Service\API\ItemManager;
use App\Service\API\RuneManager;
use App\Service\Build\BuildCatalogGate;
use App\Service\Build\BuildStructureValidator;
use App\Service\Picker\GameMode;
use App\Service\Picker\ItemOptionsProjector;
use PHPUnit\Framework\TestCase;

/**
 * Offline coverage of the gate's pure core ({@see BuildCatalogGate::evaluate}):
 * validator codes wrapped as (code, params) tuples + the game-mode availability
 * rule with its readable %items% payload. The storage-bound managers are hollow
 * shells (never invoked by evaluate) — validate()'s catalog loading is I/O glue
 * exercised by the HTTP-level flow, not unit-mocked.
 */
final class BuildCatalogGateTest extends TestCase
{
    private BuildCatalogGate $gate;

    protected function setUp(): void
    {
        $hollow = static fn (string $class): object => (new \ReflectionClass($class))->newInstanceWithoutConstructor();

        $this->gate = new BuildCatalogGate(
            new BuildStructureValidator(),
            $hollow(ChampionManager::class),
            $hollow(ItemManager::class),
            $hollow(RuneManager::class),
            new ItemOptionsProjector(),
        );
    }

    /** @return array{runeTrees: array<mixed>, championIds: list<string>, itemData: array<int, array<string, mixed>>} */
    private static function catalogs(): array
    {
        $tree = static fn (int $id, string $key, array $slots): array => [
            'id' => $id,
            'key' => $key,
            'name' => $key,
            'slots' => array_map(
                static fn (array $perkIds): array => [
                    'runes' => array_map(static fn (int $p): array => ['id' => $p, 'key' => "p$p", 'name' => "P$p"], $perkIds),
                ],
                $slots,
            ),
        ];

        return [
            'runeTrees' => [
                $tree(8000, 'Precision', [[8005, 8008], [9101, 9111], [9104, 9105], [8014, 8017]]),
                $tree(8100, 'Domination', [[8112, 8124], [8126, 8139], [8138, 8135], [8106, 8105]]),
            ],
            'championIds' => ['Aatrox', 'Ahri'],
            'itemData' => [
                1055 => ['name' => "Doran's Blade", 'gold' => ['purchasable' => true], 'maps' => [11 => true, 12 => true]],
                3006 => ['name' => 'Berserker Greaves', 'gold' => ['purchasable' => true], 'maps' => [11 => true, 12 => false]],
                2003 => ['name' => 'Health Potion', 'gold' => ['purchasable' => true]],
            ],
        ];
    }

    /** @return array<mixed> */
    private static function structure(array $items = ['1055', '2003']): array
    {
        return [
            'championId' => 'Aatrox',
            'runes' => [
                'primaryStyleId' => 8000,
                'primarySelections' => [8005, 9101, 9104, 8014],
                'secondaryStyleId' => 8100,
                'secondarySelections' => [8126, 8138],
            ],
            'steps' => [['label' => 'Start', 'note' => null, 'items' => $items]],
        ];
    }

    public function testValidStructureOnItsModeYieldsNoError(): void
    {
        self::assertSame([], $this->gate->evaluate(self::structure(), GameMode::SummonersRift, self::catalogs()));
        // No maps flag at all (2003) stays available whatever the mode.
        self::assertSame([], $this->gate->evaluate(self::structure(['2003']), GameMode::Arena, self::catalogs()));
    }

    public function testModeUnavailableItemsAreReportedByName(): void
    {
        $errors = $this->gate->evaluate(
            self::structure(['1055', '3006', '3006']),
            GameMode::Aram,
            self::catalogs(),
        );

        self::assertSame(
            [[BuildCatalogGate::ERROR_ITEM_MODE, ['%items%' => 'Berserker Greaves']]],
            $errors,
            'valid ids on the wrong map: one readable error, names deduplicated',
        );
    }

    public function testValidatorCodesAreWrappedAsParamlessTuples(): void
    {
        $structure = self::structure(['9999']);
        $structure['championId'] = 'Nobody';

        $errors = $this->gate->evaluate($structure, GameMode::SummonersRift, self::catalogs());

        self::assertContains([BuildStructureValidator::ERROR_CHAMPION_UNKNOWN, []], $errors);
        self::assertContains([BuildStructureValidator::ERROR_STEP_ITEM_UNKNOWN, []], $errors);
        // An id unknown to the dataset is a patch problem, not a mode problem.
        $codes = array_column($errors, 0);
        self::assertNotContains(BuildCatalogGate::ERROR_ITEM_MODE, $codes);
    }

    public function testModeCheckStacksOnTopOfValidatorErrors(): void
    {
        $structure = self::structure(['3006']);
        $structure['championId'] = 'Nobody';

        $errors = $this->gate->evaluate($structure, GameMode::Aram, self::catalogs());

        self::assertContains([BuildStructureValidator::ERROR_CHAMPION_UNKNOWN, []], $errors);
        self::assertContains([BuildCatalogGate::ERROR_ITEM_MODE, ['%items%' => 'Berserker Greaves']], $errors);
    }
}
