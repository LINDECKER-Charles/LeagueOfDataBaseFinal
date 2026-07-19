<?php
declare(strict_types=1);

namespace App\Service\Build;

/**
 * Pure validation core of the build "structure" contract (championId + runes +
 * steps, see {@see \App\Entity\Build} for the persisted shapes). No I/O: the
 * catalogs (rune trees in raw DDragon shape, valid champion/item ids) are passed
 * in by {@see BuildCatalogGate}, which keeps every rule unit-testable offline.
 *
 * Errors are STABLE codes doubling as translation keys (build.error.*) — the
 * controller translates them into flashes, tests assert them literally.
 */
final class BuildStructureValidator
{
    public const NAME_MIN = 3;
    public const NAME_MAX = 80;
    public const DESCRIPTION_MAX = 2000;

    public const PRIMARY_PICKS = 4;
    public const SECONDARY_PICKS = 2;
    /** Secondary picks come from the minor slots only — never from the keystone slot 0. */
    public const SECONDARY_SLOT_MIN = 1;

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
     * @param array<mixed>        $structure        decoded structure JSON
     * @param array<mixed>        $runeTrees        raw DDragon runesReforged list (5 trees, 4 slots each)
     * @param list<string>        $validChampionIds e.g. ["Aatrox", ...]
     * @param list<string>        $validItemIds     e.g. ["1055", "3006", ...]
     * @return list<string> deduplicated error codes; empty means valid
     */
    public function validate(array $structure, array $runeTrees, array $validChampionIds, array $validItemIds): array
    {
        $errors = [
            ...$this->validateChampion($structure['championId'] ?? null, $validChampionIds),
            ...$this->validateRunes($structure['runes'] ?? null, RuneTreeIndex::fromTrees($runeTrees)->slotsByTree()),
            ...$this->validateSteps($structure['steps'] ?? null, array_fill_keys($validItemIds, true)),
        ];

        return array_values(array_unique($errors));
    }

    /** @return list<string> */
    private function validateChampion(mixed $championId, array $validChampionIds): array
    {
        if (!is_string($championId) || trim($championId) === '') {
            return [self::ERROR_STRUCTURE];
        }

        return in_array(trim($championId), $validChampionIds, true) ? [] : [self::ERROR_CHAMPION_UNKNOWN];
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

        $primaryStyleId = self::readInt($runes['primaryStyleId'] ?? null);

        return [
            ...$this->validatePrimary($primaryStyleId, $runes['primarySelections'] ?? null, $slotsByTree),
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

        if (!is_array($selections) || count($selections) !== self::PRIMARY_PICKS || !array_is_list($selections)) {
            return [self::ERROR_PRIMARY_COUNT];
        }

        $errors = [];
        foreach ($selections as $slotIndex => $raw) {
            $perkId = self::readInt($raw);
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
    private function validateSecondary(array $runes, ?int $primaryStyleId, array $slotsByTree): array
    {
        $styleId = self::readInt($runes['secondaryStyleId'] ?? null);
        if ($styleId === null || !isset($slotsByTree[$styleId])) {
            return [self::ERROR_SECONDARY_STYLE];
        }
        if ($styleId === $primaryStyleId) {
            return [self::ERROR_SECONDARY_SAME_STYLE];
        }

        $selections = $runes['secondarySelections'] ?? null;
        if (!is_array($selections) || count($selections) !== self::SECONDARY_PICKS || !array_is_list($selections)) {
            return [self::ERROR_SECONDARY_COUNT];
        }

        $errors = [];
        $usedSlots = [];
        foreach ($selections as $raw) {
            $slot = $this->secondarySlotOf(self::readInt($raw), $slotsByTree[$styleId]);
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
            $errors = [...$errors, ...$this->validateStep($step, $validItems, $totalItems)];
        }
        if ($totalItems > self::TOTAL_ITEMS_MAX) {
            $errors[] = self::ERROR_STEPS_TOTAL_ITEMS;
        }

        return $errors;
    }

    /**
     * @param array<int|string, true> $validItems
     * @return list<string>
     */
    private function validateStep(mixed $step, array $validItems, int &$totalItems): array
    {
        if (!is_array($step)) {
            return [self::ERROR_STRUCTURE];
        }

        $errors = [];
        $label = $step['label'] ?? null;
        if (!is_string($label) || trim($label) === '' || mb_strlen(trim($label)) > self::STEP_LABEL_MAX) {
            $errors[] = self::ERROR_STEP_LABEL;
        }

        $note = $step['note'] ?? null;
        if ($note !== null && (!is_string($note) || mb_strlen(trim($note)) > self::STEP_NOTE_MAX)) {
            $errors[] = self::ERROR_STEP_NOTE;
        }

        $items = $step['items'] ?? null;
        if (!is_array($items) || !array_is_list($items)
            || count($items) < self::ITEMS_PER_STEP_MIN || count($items) > self::ITEMS_PER_STEP_MAX
        ) {
            return [...$errors, self::ERROR_STEP_ITEMS_COUNT];
        }

        $totalItems += count($items);
        foreach ($items as $itemId) {
            // Duplicates are legitimate (potions, stacked components) — only existence matters.
            if (!is_scalar($itemId) || !isset($validItems[(string) $itemId])) {
                $errors[] = self::ERROR_STEP_ITEM_UNKNOWN;
            }
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
            if ($slotIndex >= self::SECONDARY_SLOT_MIN && isset($perks[$perkId])) {
                return $slotIndex;
            }
        }

        return null;
    }

    /**
     * Lenient int reading shared with {@see BuildStructureNormalizer}: accepts
     * an int or an int-shaped string (JSON round-trips), nothing else.
     */
    public static function readInt(mixed $value): ?int
    {
        if (is_int($value)) {
            return $value;
        }
        if (is_string($value) && $value !== '' && (string) (int) $value === $value) {
            return (int) $value;
        }

        return null;
    }
}
