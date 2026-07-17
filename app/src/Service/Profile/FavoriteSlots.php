<?php
declare(strict_types=1);

namespace App\Service\Profile;

use App\Entity\User;
use App\Service\Picker\PickerCatalog;

/**
 * Bridges the {@see FavoriteSlot} model and its two worlds: the User entity
 * columns (read/apply) and the picker catalog (per-type resolution). The strict
 * {@see resolve()} lets save-time validation distinguish "id unknown on this
 * patch" from "data layer down"; {@see resolveAll()} is the display-time
 * best-effort — a profile page never leaks a data exception.
 */
final class FavoriteSlots
{
    public function __construct(private readonly PickerCatalog $catalog) {}

    /** @return array<string, ?string> stored id keyed by slot value */
    public function storedIds(User $user): array
    {
        return [
            FavoriteSlot::Champion->value => $user->getFavoriteChampionId(),
            FavoriteSlot::Item->value => $user->getFavoriteItemId(),
            FavoriteSlot::Rune->value => $user->getFavoriteRuneId(),
            FavoriteSlot::Summoner->value => $user->getFavoriteSummonerId(),
        ];
    }

    /** @param array<string, ?string> $values sanitized ids keyed by slot value */
    public function apply(User $user, array $values): void
    {
        $user->setFavoriteChampionId($values[FavoriteSlot::Champion->value] ?? null);
        $user->setFavoriteItemId($values[FavoriteSlot::Item->value] ?? null);
        $user->setFavoriteRuneId($values[FavoriteSlot::Rune->value] ?? null);
        $user->setFavoriteSummonerId($values[FavoriteSlot::Summoner->value] ?? null);
    }

    /**
     * Strict resolution — upstream failures bubble, callers own the fallback.
     *
     * @return ?array{id: string, name: string, image: ?string, type: string}
     */
    public function resolve(FavoriteSlot $slot, string $id, string $version, string $lang): ?array
    {
        return match ($slot) {
            FavoriteSlot::Champion => $this->catalog->resolveChampion($id, $version, $lang),
            FavoriteSlot::Item => $this->catalog->resolveItem($id, $version, $lang),
            FavoriteSlot::Rune => $this->catalog->resolveRune($id, $version, $lang),
            FavoriteSlot::Summoner => $this->catalog->resolveSummoner($id, $version, $lang),
        };
    }

    /**
     * Display state of the four slots: `current` is null both for an empty slot
     * and for a favorite unavailable on this patch (storedId tells them apart).
     *
     * @return array<string, array{storedId: ?string, current: ?array{id: string, name: string, image: ?string, type: string}}>
     */
    public function resolveAll(User $user, string $version, string $lang): array
    {
        $slots = [];
        foreach ($this->storedIds($user) as $slotValue => $storedId) {
            $slots[$slotValue] = [
                'storedId' => $storedId,
                'current' => $this->tryResolve(FavoriteSlot::from($slotValue), $storedId, $version, $lang),
            ];
        }

        return $slots;
    }

    /** @return ?array{id: string, name: string, image: ?string, type: string} */
    private function tryResolve(FavoriteSlot $slot, ?string $id, string $version, string $lang): ?array
    {
        if ($id === null || $id === '') {
            return null;
        }

        try {
            return $this->resolve($slot, $id, $version, $lang);
        } catch (\Throwable) {
            return null;
        }
    }
}
