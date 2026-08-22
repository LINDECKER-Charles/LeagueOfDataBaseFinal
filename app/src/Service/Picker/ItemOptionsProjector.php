<?php
declare(strict_types=1);

namespace App\Service\Picker;

use App\Service\API\Edition\Edition;
use App\Service\API\Edition\ItemEditionRule;

/**
 * Pure projection of the item dataset into picker options.
 *
 * Only "pickable" items are offered: purchasable, playable on the requested
 * game mode's map, not hidden from the shop, not champion-bound. Numeric JSON
 * map keys ("3006") arrive as PHP ints — every id is recast to string at the
 * boundary. Images are id-keyed upstream, so the LoL Classic twin of an item
 * (same name, own icon) can never borrow the wrong icon.
 */
final class ItemOptionsProjector
{
    public const TYPE = 'item';

    /**
     * @param array<int|string, array<string, mixed>> $data   raw item.json "data" map
     *                                                        (key = item id)
     * @param array<int|string, ?string>              $images id-keyed
     *                                                        ItemManager::getImages() result
     *                                                        (PHP recasts numeric keys to int)
     * @return list<array<string, mixed>>
     */
    public function project(array $data, array $images, GameMode $mode = GameMode::DEFAULT): array
    {
        $options = [];
        foreach ($data as $id => $entry) {
            if (!\is_array($entry) || !$this->isPickable((string) $id, $entry, $mode)) {
                continue;
            }
            $options[] = $this->option((string) $id, $entry, $images);
        }

        return PickerFormat::sortByName($options);
    }

    /**
     * Resolution is presence-based (raw dataset, unfiltered): a stored favorite
     * that became non-purchasable on this patch still displays instead of
     * pretending it vanished.
     *
     * @param array<int|string, array<string, mixed>> $data
     * @param array<int|string, ?string>              $images
     * @return ?array{id: string, name: string, image: ?string, type: string}
     */
    public function resolve(array $data, array $images, string $id): ?array
    {
        $entry = $data[$id] ?? null;
        if (!\is_array($entry)) {
            return null;
        }

        return [
            'id'    => $id,
            'name'  => (string) ($entry['name'] ?? $id),
            'image' => PickerFormat::imagePath($images[$id] ?? null),
            'type'  => self::TYPE,
        ];
    }

    /**
     * Names of the given item ids that exist in the dataset but are NOT playable
     * on the mode's map — the readable payload of a mode-availability error.
     * Ids unknown to the dataset are skipped: their absence is a patch problem,
     * reported separately by the structure validator. One label per id: a
     * classic twin is qualified by its id rather than folded into its namesake.
     *
     * @param array<int|string, array<string, mixed>> $data raw item.json "data" map
     * @param list<string>                            $itemIds
     * @return list<string> unavailable item labels, one per id, dataset order
     */
    public function unavailableOn(array $data, GameMode $mode, array $itemIds): array
    {
        $wanted = array_fill_keys(array_map(strval(...), $itemIds), true);

        $labels = [];
        foreach ($data as $id => $entry) {
            $id = (string) $id;
            if (
                !isset($wanted[$id])
                || !\is_array($entry)
                || $this->isAvailableOn($id, $entry, $mode)
            ) {
                continue;
            }
            $labels[] = ItemEditionRule::qualifiedName($id, (string) ($entry['name'] ?? $id));
        }

        // Two current-game namesakes (Arena variants) would read as a stutter —
        // one mention per label is enough for the player to act on.
        return array_values(array_unique($labels));
    }

    /**
     * Whether an item id exists in the dataset AND is playable on the mode's map —
     * i.e. an old build may carry it to this (version, mode). Both "removed from
     * the patch" and "not on this mode's map" answer false.
     *
     * @param array<int|string, array<string, mixed>> $data raw item.json "data" map
     */
    public function isPlayable(array $data, GameMode $mode, string $id): bool
    {
        $entry = $data[$id] ?? null;

        return \is_array($entry) && $this->isAvailableOn($id, $entry, $mode);
    }

    /** @param array<string, mixed> $entry */
    private function isPickable(string $id, array $entry, GameMode $mode): bool
    {
        return ($entry['gold']['purchasable'] ?? false) === true
            && $this->isAvailableOn($id, $entry, $mode)
            && ($entry['hideFromAll'] ?? false) !== true
            && (string) ($entry['requiredChampion'] ?? '') === '';
    }

    /**
     * Playable on the mode's map AND from the current game: every build mode is
     * a current-game queue, so the LoL Classic catalogue is never offered —
     * item.json flags most classic items on map 12 (ARAM) although they only
     * exist in the classic client.
     *
     * A missing "maps" flag means "not excluded" — older item.json versions
     * predate some maps and must not blank the whole catalog.
     *
     * @param array<string, mixed> $entry
     */
    private function isAvailableOn(string $id, array $entry, GameMode $mode): bool
    {
        return ItemEditionRule::of($id) === Edition::Modern
            && ($entry['maps'][$mode->mapId()] ?? true) !== false;
    }

    /**
     * The picker contract, and nothing more: the island filters on name/tags/gold
     * and renders the icon. Recipe data (from/into/depth) belongs to the item
     * detail page, which builds its craft tree from `recipeTree`, not from here.
     *
     * @param array<string, mixed>       $entry
     * @param array<int|string, ?string> $images
     * @return array{id: string, name: string, image: ?string, gold: int, tags: list<string>}
     */
    private function option(string $id, array $entry, array $images): array
    {
        return [
            'id'    => $id,
            'name'  => (string) ($entry['name'] ?? $id),
            'image' => PickerFormat::imagePath($images[$id] ?? null),
            'gold'  => (int) ($entry['gold']['total'] ?? 0),
            'tags'  => array_values(array_map(strval(...), (array) ($entry['tags'] ?? []))),
        ];
    }
}
