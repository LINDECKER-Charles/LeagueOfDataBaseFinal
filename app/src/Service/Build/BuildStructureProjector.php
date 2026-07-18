<?php

declare(strict_types=1);

namespace App\Service\Build;

use App\Service\Picker\GameMode;
use App\Service\Picker\ItemOptionsProjector;

/**
 * Forward-ports (or back-ports) a stored build structure onto another patch,
 * keeping only the components that still exist — and are usable — on the target,
 * and reporting what had to be dropped. This is the "soft" cross-version import:
 * where {@see BuildCatalogGate} rejects a structure wholesale, this salvages the
 * compatible parts so the visitor lands in the editor with a working draft.
 *
 * Pure: catalogs (champion ids, rune trees, item data) are passed in by the
 * caller ({@see BuildCatalogGate::catalogs}), which keeps it unit-testable offline.
 * It is best-effort by design — the normal write pipeline stays the final gate,
 * so a champion that must be re-picked or an emptied step is fixed there, not here.
 */
final class BuildStructureProjector
{
    /** A rune page that carried nothing to the target — the editor treats it as unset. */
    private const BLANK_RUNES = [
        'primaryStyleId' => 0,
        'primarySelections' => [],
        'secondaryStyleId' => 0,
        'secondarySelections' => [],
    ];

    public function __construct(
        private readonly ItemOptionsProjector $itemProjector,
    ) {}

    /**
     * @param array{championId?: mixed, runes?: mixed, steps?: mixed}                       $structure
     * @param array{runeTrees: array<mixed>, championIds: list<int|string>, itemData: array<int|string, mixed>} $catalogs
     * @return array{
     *   structure: array{championId: string, runes: array<mixed>, steps: list<array<mixed>>},
     *   report: array{championMissing: bool, runesReset: bool, droppedItems: list<array{step: int, id: string, name: string}>}
     * }
     */
    public function project(array $structure, GameMode $mode, array $catalogs): array
    {
        $championId = trim((string) ($structure['championId'] ?? ''));
        $championIds = array_map(strval(...), $catalogs['championIds']);

        [$runes, $runesReset] = $this->projectRunes($structure['runes'] ?? null, $catalogs['runeTrees']);
        [$steps, $droppedItems] = $this->projectSteps($structure['steps'] ?? null, $mode, $catalogs['itemData']);

        return [
            'structure' => ['championId' => $championId, 'runes' => $runes, 'steps' => $steps],
            'report' => [
                'championMissing' => $championId === '' || !in_array($championId, $championIds, true),
                'runesReset' => $runesReset,
                'droppedItems' => $droppedItems,
            ],
        ];
    }

    /**
     * Rune pages are cohesive (4 primary from ordered slots + 2 secondary): a
     * partial carry-over would be structurally invalid, so it is all-or-reset —
     * kept verbatim when every selected id still exists on the target, else blanked.
     *
     * @param array<mixed> $runeTrees
     * @return array{0: array<mixed>, 1: bool}
     */
    private function projectRunes(mixed $runes, array $runeTrees): array
    {
        $ids = $this->runeIdsOf($runes);
        if ($ids === []) {
            return [self::BLANK_RUNES, false]; // never configured — not a "reset"
        }

        $valid = $this->runeIdSet($runeTrees);
        foreach ($ids as $id) {
            if (!isset($valid[$id])) {
                return [self::BLANK_RUNES, true];
            }
        }

        return [(array) $runes, false];
    }

    /**
     * @param array<int|string, mixed> $itemData
     * @return array{0: list<array<mixed>>, 1: list<array{step: int, id: string, name: string}>}
     */
    private function projectSteps(mixed $steps, GameMode $mode, array $itemData): array
    {
        if (!is_array($steps)) {
            return [[], []];
        }

        $kept = [];
        $dropped = [];
        foreach (array_values($steps) as $index => $step) {
            if (!is_array($step)) {
                continue;
            }
            $filtered = [];
            foreach ($this->itemIdsOf($step) as $id) {
                if ($this->itemProjector->isPlayable($itemData, $mode, $id)) {
                    $filtered[] = $id;
                } else {
                    $dropped[] = ['step' => $index, 'id' => $id, 'name' => $this->itemName($itemData, $id)];
                }
            }
            if ($filtered !== []) {
                $kept[] = ['label' => (string) ($step['label'] ?? ''), 'note' => $step['note'] ?? null, 'items' => $filtered];
            }
        }

        return [$kept, $dropped];
    }

    /** @param array<mixed> $step @return list<string> */
    private function itemIdsOf(array $step): array
    {
        $ids = array_filter((array) ($step['items'] ?? []), 'is_scalar');

        return array_map(strval(...), array_values($ids));
    }

    /** @param array<int|string, mixed> $itemData */
    private function itemName(array $itemData, string $id): string
    {
        $entry = $itemData[$id] ?? null;

        return is_array($entry) && isset($entry['name']) ? (string) $entry['name'] : $id;
    }

    /** Every style and perk id present in the target trees. @param array<mixed> $runeTrees @return array<int, true> */
    private function runeIdSet(array $runeTrees): array
    {
        $set = [];
        foreach ($runeTrees as $tree) {
            if (($treeId = BuildStructureValidator::readInt($tree['id'] ?? null)) !== null) {
                $set[$treeId] = true;
            }
            foreach ((array) ($tree['slots'] ?? []) as $slot) {
                foreach ((array) ($slot['runes'] ?? []) as $rune) {
                    if (($perkId = BuildStructureValidator::readInt($rune['id'] ?? null)) !== null) {
                        $set[$perkId] = true;
                    }
                }
            }
        }

        return $set;
    }

    /** Every id referenced by a rune page (styles + selections). @return list<int> */
    private function runeIdsOf(mixed $runes): array
    {
        if (!is_array($runes)) {
            return [];
        }

        $ids = [];
        foreach (['primaryStyleId', 'secondaryStyleId'] as $key) {
            if (($id = BuildStructureValidator::readInt($runes[$key] ?? null)) !== null) {
                $ids[] = $id;
            }
        }
        foreach (['primarySelections', 'secondarySelections'] as $key) {
            foreach ((array) ($runes[$key] ?? []) as $raw) {
                if (($id = BuildStructureValidator::readInt($raw)) !== null) {
                    $ids[] = $id;
                }
            }
        }

        return $ids;
    }
}
