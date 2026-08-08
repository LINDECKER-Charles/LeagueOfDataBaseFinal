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
     * Sentinel written when a style/perk id cannot be read: the editor reads 0 as
     * "nothing chosen yet" (same meaning as {@see BuildStructureProjector} blank
     * pages), and the validator rejects it on the next write.
     */
    public const UNSET_PERK_ID = 0;

    /**
     * @param array<mixed> $structure
     * @return array{
     *     championId: string,
     *     runes: array<string, mixed>,
     *     steps: list<array<string, mixed>>
     * }
     */
    public function normalize(array $structure): array
    {
        return [
            'championId' => trim((string) ($structure['championId'] ?? '')),
            'runes' => $this->normalizeRunes($structure['runes'] ?? null),
            'steps' => array_values(array_map(
                $this->normalizeStep(...),
                is_array($structure['steps'] ?? null) ? $structure['steps'] : [],
            )),
        ];
    }

    /**
     * @return array{
     *     primaryStyleId: int,
     *     primarySelections: list<int>,
     *     secondaryStyleId: int,
     *     secondarySelections: list<int>
     * }
     */
    private function normalizeRunes(mixed $runes): array
    {
        $runes = is_array($runes) ? $runes : [];

        return [
            'primaryStyleId' => $this->toPerkId($runes['primaryStyleId'] ?? null),
            'primarySelections' => $this->toIntList($runes['primarySelections'] ?? []),
            'secondaryStyleId' => $this->toPerkId($runes['secondaryStyleId'] ?? null),
            'secondarySelections' => $this->toIntList($runes['secondarySelections'] ?? []),
        ];
    }

    private function toPerkId(mixed $value): int
    {
        return IntegerValue::read($value) ?? self::UNSET_PERK_ID;
    }

    /** @return list<int> */
    private function toIntList(mixed $values): array
    {
        if (!is_array($values)) {
            return [];
        }

        return array_values(array_map($this->toPerkId(...), $values));
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
