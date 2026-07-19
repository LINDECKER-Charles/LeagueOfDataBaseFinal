<?php
declare(strict_types=1);

namespace App\Service\Profile;

/**
 * Sanitizes a submitted favorite selection against the current (version, lang)
 * dataset. Pure core: existence is an injected predicate, so the policy is
 * testable without the data layer:
 *   - empty clears silently;
 *   - oversized ids are dropped WITH a per-slot warning;
 *   - an id that exists on the current patch passes through;
 *   - an id absent from the current patch but EQUAL to the currently stored one
 *     is preserved untouched (it is the user's existing favorite, merely not on
 *     the version being viewed — wiping it would be silent data loss);
 *   - any other unknown id is dropped WITH a per-slot warning.
 *
 * Existence failures (upstream down) are deliberately not caught here: wiping
 * favorites on a transient outage would be data loss, the caller decides.
 */
final class FavoriteSelectionSanitizer
{
    /**
     * @param array<string, ?string>               $submitted raw ids keyed by slot value ({@see FavoriteSlot})
     * @param array<string, ?string>               $stored    the user's currently persisted ids, keyed by slot value
     * @param callable(FavoriteSlot, string): bool $exists    does this id exist on the current patch?
     * @return array{values: array<string, ?string>, invalid: list<FavoriteSlot>}
     */
    public function sanitize(array $submitted, array $stored, callable $exists): array
    {
        $values = [];
        $invalid = [];
        foreach (FavoriteSlot::cases() as $slot) {
            $id = trim((string) ($submitted[$slot->value] ?? ''));
            if ($id === '') {
                $values[$slot->value] = null;
                continue;
            }
            if (mb_strlen($id) > $slot->maxLength()) {
                $values[$slot->value] = null;
                $invalid[] = $slot;
                continue;
            }
            if ($exists($slot, $id)) {
                $values[$slot->value] = $id;
                continue;
            }
            // Absent from this patch: keep it only when it is the unchanged
            // stored favorite (off-patch, not gone); drop a genuinely bad pick.
            if ($id === ($stored[$slot->value] ?? null)) {
                $values[$slot->value] = $id;
                continue;
            }
            $values[$slot->value] = null;
            $invalid[] = $slot;
        }

        return ['values' => $values, 'invalid' => $invalid];
    }
}
