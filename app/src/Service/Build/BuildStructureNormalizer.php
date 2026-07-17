<?php
declare(strict_types=1);

namespace App\Service\Build;

/**
 * Canonicalizes an already-validated build structure into the persisted shape
 * documented on {@see \App\Entity\Build}: numeric-string perk ids cast to int
 * (JSON round-trips), labels trimmed, blank notes nulled, item ids cast to
 * string (PHP recasts numeric JSON keys/values to int).
 *
 * Pure and lenient on malformed input (defensive casts) — correctness is the
 * validator's job, canonical form is this class's only concern.
 */
final class BuildStructureNormalizer
{
    /**
     * @param array<mixed> $structure
     * @return array{championId: string, runes: array<string, mixed>, steps: list<array<string, mixed>>}
     */
    public function normalize(array $structure): array
    {
        $runes = is_array($structure['runes'] ?? null) ? $structure['runes'] : [];

        return [
            'championId' => trim((string) ($structure['championId'] ?? '')),
            'runes' => [
                'primaryStyleId' => (int) BuildStructureValidator::readInt($runes['primaryStyleId'] ?? null),
                'primarySelections' => $this->toIntList($runes['primarySelections'] ?? []),
                'secondaryStyleId' => (int) BuildStructureValidator::readInt($runes['secondaryStyleId'] ?? null),
                'secondarySelections' => $this->toIntList($runes['secondarySelections'] ?? []),
            ],
            'steps' => array_values(array_map(
                $this->normalizeStep(...),
                is_array($structure['steps'] ?? null) ? $structure['steps'] : [],
            )),
        ];
    }

    /** @return list<int> */
    private function toIntList(mixed $values): array
    {
        if (!is_array($values)) {
            return [];
        }

        return array_values(array_map(
            static fn (mixed $v): int => (int) BuildStructureValidator::readInt($v),
            $values,
        ));
    }

    /**
     * @return array{label: string, note: ?string, items: list<string>}
     */
    private function normalizeStep(mixed $step): array
    {
        $step = is_array($step) ? $step : [];
        $note = is_string($step['note'] ?? null) ? trim($step['note']) : null;
        $items = is_array($step['items'] ?? null) ? $step['items'] : [];

        return [
            'label' => trim((string) ($step['label'] ?? '')),
            'note' => $note === '' ? null : $note,
            'items' => array_values(array_map(
                static fn (mixed $id): string => is_scalar($id) ? (string) $id : '',
                $items,
            )),
        ];
    }
}
