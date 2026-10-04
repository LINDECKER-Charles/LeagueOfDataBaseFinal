<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\Build\BuildStructureValidator;

/**
 * Envelope rules: what the validator makes of the structure itself (missing or
 * malformed sections, unknown champion) and how per-section codes accumulate.
 * The detailed rules live in the sibling suites
 * {@see BuildStructureValidatorRunesTest} and {@see BuildStructureValidatorStepsTest}.
 */
final class BuildStructureValidatorTest extends BuildStructureValidatorTestCase
{
    public function testValidStructurePasses(): void
    {
        self::assertSame([], $this->validate(self::base()));
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
