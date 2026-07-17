<?php
declare(strict_types=1);

namespace App\Service\Profile;

/**
 * Sanitizes a submitted favorite selection against the current (version, lang)
 * dataset. Pure core: existence is an injected predicate, so the policy —
 * empty clears, oversized or unknown ids are dropped WITH a per-slot warning,
 * valid ids pass through — is testable without the data layer. Existence
 * failures (upstream down) are deliberately not caught here: wiping favorites
 * on a transient outage would be data loss, the caller decides.
 */
final class FavoriteSelectionSanitizer
{
    /**
     * @param array<string, ?string>                $submitted raw ids keyed by slot value ({@see FavoriteSlot})
     * @param callable(FavoriteSlot, string): bool $exists    does this id exist on the current patch?
     * @return array{values: array<string, ?string>, invalid: list<FavoriteSlot>}
     */
    public function sanitize(array $submitted, callable $exists): array
    {
        $values = [];
        $invalid = [];
        foreach (FavoriteSlot::cases() as $slot) {
            $id = trim((string) ($submitted[$slot->value] ?? ''));
            if ($id === '') {
                $values[$slot->value] = null;
                continue;
            }
            if (mb_strlen($id) > $slot->maxLength() || !$exists($slot, $id)) {
                $values[$slot->value] = null;
                $invalid[] = $slot;
                continue;
            }
            $values[$slot->value] = $id;
        }

        return ['values' => $values, 'invalid' => $invalid];
    }
}
