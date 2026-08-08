<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\Build\BuildStructureValidator;

/**
 * The rune-page rules: style existence, selection counts, and the slot/tree
 * reachability of every perk (primary path and secondary path).
 */
final class BuildStructureValidatorRunesTest extends BuildStructureValidatorTestCase
{
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

        self::assertSame(
            [BuildStructureValidator::ERROR_SECONDARY_SAME_STYLE],
            $this->validate($s),
        );
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
}
