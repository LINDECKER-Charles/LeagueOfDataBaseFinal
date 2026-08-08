<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\Build\BuildCatalogs;
use App\Service\Build\BuildStructureValidator;
use PHPUnit\Framework\TestCase;

/**
 * Shared ground for the structure-rule suites: minimal DDragon-shaped fixtures
 * (3 trees x 4 slots — slot 0 = keystones, like upstream) plus the one call
 * that runs the validator against them.
 */
abstract class BuildStructureValidatorTestCase extends TestCase
{
    protected const CHAMPIONS = ['Aatrox', 'Ahri'];
    protected const ITEMS = ['1055', '2003', '3006', '3031'];

    private BuildStructureValidator $validator;

    protected function setUp(): void
    {
        $this->validator = new BuildStructureValidator();
    }

    /** @return array<mixed> three 4-slot trees; ids mirror live Precision/Domination/Sorcery */
    protected static function trees(): array
    {
        $tree = static fn (int $id, string $key, array $slots): array => [
            'id' => $id,
            'key' => $key,
            'icon' => "perk-images/Styles/$key.png",
            'name' => $key,
            'slots' => array_map(
                static fn (array $perkIds): array => [
                    'runes' => array_map(
                        static fn (int $perkId): array => [
                            'id' => $perkId,
                            'key' => "p$perkId",
                            'icon' => 'x.png',
                            'name' => "P$perkId",
                        ],
                        $perkIds,
                    ),
                ],
                $slots,
            ),
        ];

        return [
            $tree(8000, 'Precision', [[8005, 8008], [9101, 9111], [9104, 9105], [8014, 8017]]),
            $tree(8100, 'Domination', [[8112, 8124], [8126, 8139], [8138, 8135], [8106, 8105]]),
            $tree(8200, 'Sorcery', [[8214, 8229], [8224, 8226], [8210, 8234], [8237, 8232]]),
        ];
    }

    /** @return array<mixed> a fully valid structure to mutate per test */
    protected static function base(): array
    {
        return [
            'championId' => 'Aatrox',
            'runes' => [
                'primaryStyleId' => 8000,
                'primarySelections' => [8005, 9101, 9104, 8014],
                'secondaryStyleId' => 8100,
                'secondarySelections' => [8126, 8138],
            ],
            'steps' => [
                ['label' => 'Start', 'note' => null, 'items' => ['1055', '2003']],
                ['label' => 'Core', 'note' => 'rush it', 'items' => ['3006', '3031']],
            ],
        ];
    }

    /** @return list<string> */
    protected function validate(array $structure): array
    {
        return $this->validator->validate(
            $structure,
            BuildCatalogs::of(self::trees(), self::CHAMPIONS, array_fill_keys(self::ITEMS, [])),
        );
    }
}
