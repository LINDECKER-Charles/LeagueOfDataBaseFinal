<?php
declare(strict_types=1);

namespace App\Service\Picker;

/**
 * Pure projection of the item dataset into picker options.
 *
 * Only "pickable" items are offered: purchasable, playable on the requested
 * game mode's map, not hidden from the shop, not champion-bound. Numeric JSON
 * map keys ("3006") arrive as PHP ints — every id is recast to string at the
 * boundary. Image resolution is name-keyed upstream, so a name collision
 * degrades to null (placeholder) rather than showing the wrong icon.
 */
final class ItemOptionsProjector
{
    public const TYPE = 'item';

    /**
     * @param array<int|string, array<string, mixed>> $data   raw item.json "data" map (key = item id)
     * @param array<string, ?string>                  $images name-keyed ItemManager::getImages() result
     * @return list<array<string, mixed>>
     */
    public function project(array $data, array $images, GameMode $mode = GameMode::DEFAULT): array
    {
        $options = [];
        foreach ($data as $id => $entry) {
            if (!\is_array($entry) || !$this->isPickable($entry, $mode)) {
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
     * @param array<string, ?string>                  $images
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
            'image' => $this->imageOf($entry, $images),
            'type'  => self::TYPE,
        ];
    }

    /**
     * Names of the given item ids that exist in the dataset but are NOT playable
     * on the mode's map — the readable payload of a mode-availability error.
     * Ids unknown to the dataset are skipped: their absence is a patch problem,
     * reported separately by the structure validator.
     *
     * @param array<int|string, array<string, mixed>> $data raw item.json "data" map
     * @param list<string>                            $itemIds
     * @return list<string> unavailable item names, deduplicated, dataset order
     */
    public function unavailableOn(array $data, GameMode $mode, array $itemIds): array
    {
        $wanted = array_fill_keys(array_map(strval(...), $itemIds), true);

        $names = [];
        foreach ($data as $id => $entry) {
            if (!isset($wanted[(string) $id]) || !\is_array($entry) || $this->isOnMap($entry, $mode)) {
                continue;
            }
            $names[(string) ($entry['name'] ?? $id)] = true;
        }

        return array_keys($names);
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

        return \is_array($entry) && $this->isOnMap($entry, $mode);
    }

    /** @param array<string, mixed> $entry */
    private function isPickable(array $entry, GameMode $mode): bool
    {
        return ($entry['gold']['purchasable'] ?? false) === true
            && $this->isOnMap($entry, $mode)
            && ($entry['hideFromAll'] ?? false) !== true
            && (string) ($entry['requiredChampion'] ?? '') === '';
    }

    /**
     * A missing "maps" flag means "not excluded" — older item.json versions
     * predate some maps and must not blank the whole catalog.
     *
     * @param array<string, mixed> $entry
     */
    private function isOnMap(array $entry, GameMode $mode): bool
    {
        return ($entry['maps'][$mode->mapId()] ?? true) !== false;
    }

    /**
     * @param array<string, mixed>   $entry
     * @param array<string, ?string> $images
     * @return array<string, mixed>
     */
    private function option(string $id, array $entry, array $images): array
    {
        return [
            'id'          => $id,
            'name'        => (string) ($entry['name'] ?? $id),
            'image'       => $this->imageOf($entry, $images),
            'gold'        => (int) ($entry['gold']['total'] ?? 0),
            'purchasable' => true,
            'tags'        => array_values(array_map(strval(...), (array) ($entry['tags'] ?? []))),
            'from'        => array_values(array_unique(array_map(strval(...), (array) ($entry['from'] ?? [])))),
            'into'        => array_values(array_map(strval(...), (array) ($entry['into'] ?? []))),
            'depth'       => isset($entry['depth']) ? (int) $entry['depth'] : null,
        ];
    }

    /**
     * @param array<string, mixed>   $entry
     * @param array<string, ?string> $images
     */
    private function imageOf(array $entry, array $images): ?string
    {
        $name = $entry['name'] ?? null;

        return \is_string($name) ? PickerFormat::imagePath($images[$name] ?? null) : null;
    }
}
