<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\Build\BuildStructureValidator;
use PHPUnit\Framework\TestCase;

/**
 * Exhaustive offline coverage of the structure rules, on minimal DDragon-shaped
 * fixtures (3 trees x 4 slots — slot 0 = keystones, like upstream).
 */
final class BuildStructureValidatorTest extends TestCase
{
    private const CHAMPIONS = ['Aatrox', 'Ahri'];
    private const ITEMS = ['1055', '2003', '3006', '3031'];

    private BuildStructureValidator $validator;

    protected function setUp(): void
    {
        $this->validator = new BuildStructureValidator();
    }

    /** @return array<mixed> three 4-slot trees; ids mirror live Precision/Domination/Sorcery */
    private static function trees(): array
    {
        $tree = static fn (int $id, string $key, array $slots): array => [
            'id' => $id,
            'key' => $key,
            'icon' => "perk-images/Styles/$key.png",
            'name' => $key,
            'slots' => array_map(
                static fn (array $perkIds): array => [
                    'runes' => array_map(
                        static fn (int $perkId): array => ['id' => $perkId, 'key' => "p$perkId", 'icon' => 'x.png', 'name' => "P$perkId"],
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
    private static function base(): array
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
    private function validate(array $structure): array
    {
        return $this->validator->validate($structure, self::trees(), self::CHAMPIONS, self::ITEMS);
    }

    public function testValidStructurePasses(): void
    {
        self::assertSame([], $this->validate(self::base()));
    }

    public function testIntShapedStringsAreAccepted(): void
    {
        $s = self::base();
        $s['runes'] = [
            'primaryStyleId' => '8000',
            'primarySelections' => ['8005', '9101', '9104', '8014'],
            'secondaryStyleId' => '8100',
            'secondarySelections' => ['8126', '8138'],
        ];

        self::assertSame([], $this->validate($s));
    }

    public function testNonNumericPerkIsASlotError(): void
    {
        $s = self::base();
        $s['runes']['primarySelections'] = [8005, 'abc', 9104, 8014];

        self::assertSame([BuildStructureValidator::ERROR_PRIMARY_SLOT], $this->validate($s));
    }

    public function testMissingChampionIsAStructureError(): void
    {
        $s = self::base();
        unset($s['championId']);

        self::assertContains(BuildStructureValidator::ERROR_STRUCTURE, $this->validate($s));
    }

    public function testUnknownChampion(): void
    {
        $s = self::base();
        $s['championId'] = 'Teemo';

        self::assertSame([BuildStructureValidator::ERROR_CHAMPION_UNKNOWN], $this->validate($s));
    }

    public function testRunesNotAnArrayIsAStructureError(): void
    {
        $s = self::base();
        $s['runes'] = 'nope';

        self::assertContains(BuildStructureValidator::ERROR_STRUCTURE, $this->validate($s));
    }

    public function testUnknownPrimaryStyle(): void
    {
        $s = self::base();
        $s['runes']['primaryStyleId'] = 9999;

        self::assertContains(BuildStructureValidator::ERROR_PRIMARY_STYLE, $this->validate($s));
    }

    public function testPrimarySelectionCountMustBeExactlyFour(): void
    {
        foreach ([[8005, 9101, 9104], [8005, 9101, 9104, 8014, 8017]] as $selections) {
            $s = self::base();
            $s['runes']['primarySelections'] = $selections;

            self::assertSame([BuildStructureValidator::ERROR_PRIMARY_COUNT], $this->validate($s));
        }
    }

    public function testPrimarySelectionsMustBeAList(): void
    {
        $s = self::base();
        $s['runes']['primarySelections'] = [1 => 8005, 2 => 9101, 3 => 9104, 4 => 8014];

        self::assertSame([BuildStructureValidator::ERROR_PRIMARY_COUNT], $this->validate($s));
    }

    public function testPrimaryPerkFromTheWrongSlotIsRejected(): void
    {
        $s = self::base();
        // 9104 lives in slot 2, offered here at slot 1.
        $s['runes']['primarySelections'] = [8005, 9104, 9104, 8014];

        self::assertSame([BuildStructureValidator::ERROR_PRIMARY_SLOT], $this->validate($s));
    }

    public function testMinorRuneCannotSitInTheKeystoneSlot(): void
    {
        $s = self::base();
        $s['runes']['primarySelections'] = [9101, 9101, 9104, 8014];

        self::assertSame([BuildStructureValidator::ERROR_PRIMARY_SLOT], $this->validate($s));
    }

    public function testPrimaryPerkFromAnotherTreeIsRejected(): void
    {
        $s = self::base();
        // 8126 belongs to Domination, not to the Precision primary tree.
        $s['runes']['primarySelections'] = [8005, 8126, 9104, 8014];

        self::assertSame([BuildStructureValidator::ERROR_PRIMARY_SLOT], $this->validate($s));
    }

    public function testUnknownSecondaryStyle(): void
    {
        $s = self::base();
        $s['runes']['secondaryStyleId'] = 4242;

        self::assertSame([BuildStructureValidator::ERROR_SECONDARY_STYLE], $this->validate($s));
    }

    public function testSecondaryStyleMustDifferFromPrimary(): void
    {
        $s = self::base();
        $s['runes']['secondaryStyleId'] = 8000;
        $s['runes']['secondarySelections'] = [9101, 9104];

        self::assertSame([BuildStructureValidator::ERROR_SECONDARY_SAME_STYLE], $this->validate($s));
    }

    public function testSecondarySelectionCountMustBeExactlyTwo(): void
    {
        foreach ([[8126], [8126, 8138, 8106]] as $selections) {
            $s = self::base();
            $s['runes']['secondarySelections'] = $selections;

            self::assertSame([BuildStructureValidator::ERROR_SECONDARY_COUNT], $this->validate($s));
        }
    }

    public function testKeystoneIsForbiddenInSecondary(): void
    {
        $s = self::base();
        // 8112 is a Domination KEYSTONE (slot 0) — unreachable from the secondary path.
        $s['runes']['secondarySelections'] = [8112, 8138];

        self::assertSame([BuildStructureValidator::ERROR_SECONDARY_SLOT], $this->validate($s));
    }

    public function testSecondaryPerkMustBelongToTheSecondaryTree(): void
    {
        $s = self::base();
        // 9101 is a Precision minor — wrong tree.
        $s['runes']['secondarySelections'] = [9101, 8138];

        self::assertSame([BuildStructureValidator::ERROR_SECONDARY_SLOT], $this->validate($s));
    }

    public function testSecondaryPicksFromTheSameSlotAreRejected(): void
    {
        $s = self::base();
        // 8126 and 8139 both live in Domination slot 1.
        $s['runes']['secondarySelections'] = [8126, 8139];

        self::assertSame([BuildStructureValidator::ERROR_SECONDARY_SAME_SLOT], $this->validate($s));
    }

    public function testSecondaryPicksFromTwoDistinctSlotsPass(): void
    {
        $s = self::base();
        $s['runes']['secondarySelections'] = [8139, 8135]; // slots 1 + 2

        self::assertSame([], $this->validate($s));
    }

    public function testStepCountBounds(): void
    {
        foreach ([[], array_fill(0, 11, ['label' => 'S', 'note' => null, 'items' => ['1055']]), 'nope'] as $steps) {
            $s = self::base();
            $s['steps'] = $steps;

            self::assertSame([BuildStructureValidator::ERROR_STEPS_COUNT], $this->validate($s));
        }
    }

    public function testStepLabelIsRequiredAndBounded(): void
    {
        foreach (['', '   ', str_repeat('x', 41), null] as $label) {
            $s = self::base();
            $s['steps'][0]['label'] = $label;

            self::assertSame([BuildStructureValidator::ERROR_STEP_LABEL], $this->validate($s));
        }
    }

    public function testStepNoteIsBoundedButOptional(): void
    {
        $s = self::base();
        $s['steps'][0]['note'] = str_repeat('n', 301);
        self::assertSame([BuildStructureValidator::ERROR_STEP_NOTE], $this->validate($s));

        $s['steps'][0]['note'] = str_repeat('n', 300);
        self::assertSame([], $this->validate($s));
    }

    public function testItemsPerStepBounds(): void
    {
        foreach ([[], array_fill(0, 9, '1055')] as $items) {
            $s = self::base();
            $s['steps'][0]['items'] = $items;

            self::assertSame([BuildStructureValidator::ERROR_STEP_ITEMS_COUNT], $this->validate($s));
        }
    }

    public function testUnknownItemIsRejected(): void
    {
        $s = self::base();
        $s['steps'][0]['items'] = ['1055', '9999'];

        self::assertSame([BuildStructureValidator::ERROR_STEP_ITEM_UNKNOWN], $this->validate($s));
    }

    public function testDuplicateItemsAreLegitimate(): void
    {
        $s = self::base();
        $s['steps'][0]['items'] = ['2003', '2003', '2003'];

        self::assertSame([], $this->validate($s));
    }

    public function testIntItemIdsAreAccepted(): void
    {
        $s = self::base();
        $s['steps'][0]['items'] = [1055, 2003];

        self::assertSame([], $this->validate($s));
    }

    public function testTotalItemsCap(): void
    {
        $step = static fn (int $n): array => ['label' => "S$n", 'note' => null, 'items' => array_fill(0, 8, '1055')];
        $s = self::base();
        // 5 steps x 8 = 40 (the cap) + one extra item.
        $s['steps'] = [...array_map($step, [1, 2, 3, 4, 5]), ['label' => 'S6', 'note' => null, 'items' => ['3006']]];

        self::assertSame([BuildStructureValidator::ERROR_STEPS_TOTAL_ITEMS], $this->validate($s));
    }

    public function testErrorsAccumulateAcrossSectionsAndDeduplicate(): void
    {
        $s = self::base();
        $s['championId'] = 'Teemo';
        // Two wrong-slot picks -> the code appears ONCE (deduplicated).
        $s['runes']['primarySelections'] = [8005, 9104, 9101, 8014];
        $s['steps'][0]['items'] = ['9999', '8888'];

        self::assertSame(
            [
                BuildStructureValidator::ERROR_CHAMPION_UNKNOWN,
                BuildStructureValidator::ERROR_PRIMARY_SLOT,
                BuildStructureValidator::ERROR_STEP_ITEM_UNKNOWN,
            ],
            $this->validate($s),
        );
    }

    public function testEmptyStructureReportsEverySection(): void
    {
        $errors = $this->validate([]);

        self::assertContains(BuildStructureValidator::ERROR_STRUCTURE, $errors);
        self::assertContains(BuildStructureValidator::ERROR_STEPS_COUNT, $errors);
    }
}
