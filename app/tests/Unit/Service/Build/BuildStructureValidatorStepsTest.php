<?php
declare(strict_types=1);

namespace App\Tests\Unit\Service\Build;

use App\Service\Build\BuildStructureValidator;

/**
 * The build-order rules: how many steps, what a step's label/note may hold,
 * and the per-step + overall item budgets.
 */
final class BuildStructureValidatorStepsTest extends BuildStructureValidatorTestCase
{
    public function testStepCountBounds(): void
    {
        foreach ([
            [],
            array_fill(0, 11, ['label' => 'S', 'note' => null, 'items' => ['1055']]),
            'nope',
        ] as $steps) {
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

            self::assertSame(
                [BuildStructureValidator::ERROR_STEP_ITEMS_COUNT],
                $this->validate($s),
            );
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
        $s = self::base();
        // 5 steps x 8 = 40 (the cap) + one extra item.
        $s['steps'] = [
            ...self::cappedSteps(),
            ['label' => 'S6', 'note' => null, 'items' => ['3006']],
        ];

        self::assertSame([BuildStructureValidator::ERROR_STEPS_TOTAL_ITEMS], $this->validate($s));
    }

    public function testAnOverfilledStepDoesNotAlsoInflateTheTotal(): void
    {
        $s = self::base();
        // 5 steps x 8 = 40 (the cap) + a 6th step already rejected on its own count:
        // its 9 items must not be added on top, or the visitor would get two errors
        // for one mistake.
        $s['steps'] = [
            ...self::cappedSteps(),
            ['label' => 'S6', 'note' => null, 'items' => array_fill(0, 9, '1055')],
        ];

        self::assertSame([BuildStructureValidator::ERROR_STEP_ITEMS_COUNT], $this->validate($s));
    }

    /**
     * Five 8-item steps — exactly the overall cap, leaving no room for more.
     *
     * @return list<array<string, mixed>>
     */
    private static function cappedSteps(): array
    {
        return array_map(
            static fn (int $n): array => [
                'label' => "S$n",
                'note' => null,
                'items' => array_fill(0, 8, '1055'),
            ],
            [1, 2, 3, 4, 5],
        );
    }
}
