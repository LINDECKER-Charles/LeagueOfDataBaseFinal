<?php
declare(strict_types=1);

namespace App\Service\Build;

/**
 * Pure validation core of the build "structure" contract (championId + runes +
 * steps, see {@see \App\Entity\Build} for the persisted shapes). No I/O: the
 * reference {@see BuildCatalogs} are loaded and passed in by
 * {@see BuildCatalogGate}, which keeps every rule unit-testable offline.
 *
 * Errors are STABLE codes doubling as translation keys (build.error.*) — the
 * controller translates them into flashes, tests assert them literally.
 */
final class BuildStructureValidator
{
    public const PRIMARY_PICKS = 4;
    public const SECONDARY_PICKS = 2;
    /** Slot 0 of every tree holds the keystones (Data Dragon contract). */
    public const KEYSTONE_SLOT = 0;
    /** Minor slots follow the keystone; secondary picks come from those only. */
    public const FIRST_MINOR_SLOT = self::KEYSTONE_SLOT + 1;

    public const STEPS_MIN = 1;
    public const STEPS_MAX = 10;
    public const STEP_LABEL_MAX = 40;
    public const STEP_NOTE_MAX = 300;
    public const ITEMS_PER_STEP_MIN = 1;
    public const ITEMS_PER_STEP_MAX = 8;
    public const TOTAL_ITEMS_MAX = 40;

    public const ERROR_STRUCTURE = 'build.error.structure.invalid';
    public const ERROR_CHAMPION_UNKNOWN = 'build.error.champion.unknown';
    public const ERROR_PRIMARY_STYLE = 'build.error.runes.primary_style';
    public const ERROR_PRIMARY_COUNT = 'build.error.runes.primary_selection_count';
    public const ERROR_PRIMARY_SLOT = 'build.error.runes.primary_selection_slot';
    public const ERROR_SECONDARY_STYLE = 'build.error.runes.secondary_style';
    public const ERROR_SECONDARY_SAME_STYLE = 'build.error.runes.secondary_same_style';
    public const ERROR_SECONDARY_COUNT = 'build.error.runes.secondary_selection_count';
    public const ERROR_SECONDARY_SLOT = 'build.error.runes.secondary_selection_slot';
    public const ERROR_SECONDARY_SAME_SLOT = 'build.error.runes.secondary_same_slot';
    public const ERROR_STEPS_COUNT = 'build.error.steps.count';
    public const ERROR_STEP_LABEL = 'build.error.steps.label';
    public const ERROR_STEP_NOTE = 'build.error.steps.note';
    public const ERROR_STEP_ITEMS_COUNT = 'build.error.steps.items_count';
    public const ERROR_STEP_ITEM_UNKNOWN = 'build.error.steps.item_unknown';
    public const ERROR_STEPS_TOTAL_ITEMS = 'build.error.steps.total_items';

    /**
     * @param array<mixed> $structure decoded structure JSON
     * @param BuildCatalogs $catalogs reference catalogs of one (version, lang)
     * @return list<string> deduplicated error codes; empty means valid
     */
    public function validate(array $structure, BuildCatalogs $catalogs): array
    {
        $errors = [
            ...$this->validateChampion(
                $structure['championId'] ?? null,
                $catalogs->championIds,
            ),
            ...$this->validateRunes(
                $structure['runes'] ?? null,
                RuneTreeIndex::fromTrees($catalogs->runeTrees)->slotsByTree(),
            ),
            ...$this->validateSteps($structure['steps'] ?? null, self::itemIdSet($catalogs)),
        ];

        return array_values(array_unique($errors));
    }

    /**
     * Item ids as a membership set. PHP recasts numeric JSON map keys to int, so
     * the ids are restored to the string form stored structures carry.
     *
     * @return array<int|string, true>
     */
    private static function itemIdSet(BuildCatalogs $catalogs): array
    {
        return array_fill_keys(array_map(strval(...), array_keys($catalogs->itemData)), true);
    }

    /** @return list<string> */
    private function validateChampion(mixed $championId, array $validChampionIds): array
    {
        if (!is_string($championId) || trim($championId) === '') {
            return [self::ERROR_STRUCTURE];
        }

        return in_array(trim($championId), $validChampionIds, true)
            ? []
            : [self::ERROR_CHAMPION_UNKNOWN];
    }

    /**
     * @param array<int, list<array<int, true>>> $slotsByTree
     * @return list<string>
     */
    private function validateRunes(mixed $runes, array $slotsByTree): array
    {
        if (!is_array($runes)) {
            return [self::ERROR_STRUCTURE];
        }

        $primaryStyleId = IntegerValue::read($runes['primaryStyleId'] ?? null);

        return [
            ...$this->validatePrimary(
                $primaryStyleId,
                $runes['primarySelections'] ?? null,
                $slotsByTree,
            ),
            ...$this->validateSecondary($runes, $primaryStyleId, $slotsByTree),
        ];
    }

    /**
     * @param array<int, list<array<int, true>>> $slotsByTree
     * @return list<string>
     */
    private function validatePrimary(?int $styleId, mixed $selections, array $slotsByTree): array
    {
        if ($styleId === null || !isset($slotsByTree[$styleId])) {
            return [self::ERROR_PRIMARY_STYLE];
        }

        if (!is_array($selections) || count($selections) !== self::PRIMARY_PICKS
            || !array_is_list($selections)
        ) {
            return [self::ERROR_PRIMARY_COUNT];
        }

        $errors = [];
        foreach ($selections as $slotIndex => $raw) {
            $perkId = IntegerValue::read($raw);
            // Selection i must be a perk of slot i of the primary tree (slot 0 = keystone).
            if ($perkId === null || !isset($slotsByTree[$styleId][$slotIndex][$perkId])) {
                $errors[] = self::ERROR_PRIMARY_SLOT;
            }
        }

        return $errors;
    }

    /**
     * @param array<mixed> $runes
     * @param array<int, list<array<int, true>>> $slotsByTree
     * @return list<string>
     */
    private function validateSecondary(
        array $runes,
        ?int $primaryStyleId,
        array $slotsByTree,
    ): array {
        $styleId = IntegerValue::read($runes['secondaryStyleId'] ?? null);
        if ($styleId === null || !isset($slotsByTree[$styleId])) {
            return [self::ERROR_SECONDARY_STYLE];
        }
        if ($styleId === $primaryStyleId) {
            return [self::ERROR_SECONDARY_SAME_STYLE];
        }

        $selections = $runes['secondarySelections'] ?? null;
        if (!is_array($selections) || count($selections) !== self::SECONDARY_PICKS
            || !array_is_list($selections)
        ) {
            return [self::ERROR_SECONDARY_COUNT];
        }

        return $this->validateSecondarySelections($selections, $slotsByTree[$styleId]);
    }

    /**
     * Both minor picks must land in distinct non-keystone slots of the secondary tree.
     *
     * @param list<mixed>            $selections
     * @param list<array<int, true>> $slots      slots of the secondary tree
     * @return list<string>
     */
    private function validateSecondarySelections(array $selections, array $slots): array
    {
        $errors = [];
        $usedSlots = [];
        foreach ($selections as $raw) {
            $slot = $this->secondarySlotOf(IntegerValue::read($raw), $slots);
            if ($slot === null) {
                $errors[] = self::ERROR_SECONDARY_SLOT;
                continue;
            }
            if (isset($usedSlots[$slot])) {
                $errors[] = self::ERROR_SECONDARY_SAME_SLOT;
            }
            $usedSlots[$slot] = true;
        }

        return $errors;
    }

    /** @param array<int|string, true> $validItems */
    private function validateSteps(mixed $steps, array $validItems): array
    {
        if (!is_array($steps) || !array_is_list($steps)
            || count($steps) < self::STEPS_MIN || count($steps) > self::STEPS_MAX
        ) {
            return [self::ERROR_STEPS_COUNT];
        }

        $errors = [];
        $totalItems = 0;
        foreach ($steps as $step) {
            $outcome = $this->validateStep($step, $validItems);
            $errors = [...$errors, ...$outcome['errors']];
            $totalItems += $outcome['itemCount'];
        }
        if ($totalItems > self::TOTAL_ITEMS_MAX) {
            $errors[] = self::ERROR_STEPS_TOTAL_ITEMS;
        }

        return $errors;
    }

    /**
     * @param array<int|string, true> $validItems
     * @return array{errors: list<string>, itemCount: int} itemCount is 0 whenever
     *         the step's own item list is already rejected: a step that breaks the
     *         per-step bounds must not also inflate the cross-step total.
     */
    private function validateStep(mixed $step, array $validItems): array
    {
        if (!is_array($step)) {
            return ['errors' => [self::ERROR_STRUCTURE], 'itemCount' => 0];
        }

        $errors = $this->validateStepText($step);
        $items = $step['items'] ?? null;
        if (!is_array($items) || !array_is_list($items)
            || count($items) < self::ITEMS_PER_STEP_MIN || count($items) > self::ITEMS_PER_STEP_MAX
        ) {
            return ['errors' => [...$errors, self::ERROR_STEP_ITEMS_COUNT], 'itemCount' => 0];
        }

        foreach ($items as $itemId) {
            // Duplicates are legitimate (potions, stacked components) — only existence matters.
            if (!is_scalar($itemId) || !isset($validItems[(string) $itemId])) {
                $errors[] = self::ERROR_STEP_ITEM_UNKNOWN;
            }
        }

        return ['errors' => $errors, 'itemCount' => count($items)];
    }

    /**
     * @param array<mixed> $step
     * @return list<string>
     */
    private function validateStepText(array $step): array
    {
        $errors = [];
        $label = $step['label'] ?? null;
        if (!is_string($label) || trim($label) === ''
            || mb_strlen(trim($label)) > self::STEP_LABEL_MAX
        ) {
            $errors[] = self::ERROR_STEP_LABEL;
        }

        $note = $step['note'] ?? null;
        if ($note !== null && (!is_string($note) || mb_strlen(trim($note)) > self::STEP_NOTE_MAX)) {
            $errors[] = self::ERROR_STEP_NOTE;
        }

        return $errors;
    }

    /**
     * Slot index (>= 1) holding this perk in the secondary tree — keystones
     * (slot 0) are deliberately unreachable from the secondary path.
     *
     * @param list<array<int, true>> $slots
     */
    private function secondarySlotOf(?int $perkId, array $slots): ?int
    {
        if ($perkId === null) {
            return null;
        }
        foreach ($slots as $slotIndex => $perks) {
            if ($slotIndex >= self::FIRST_MINOR_SLOT && isset($perks[$perkId])) {
                return $slotIndex;
            }
        }

        return null;
    }
}
