<?php
declare(strict_types=1);

namespace App\Service\Picker;

/**
 * Pure projection of the item dataset into picker options.
 *
 * Only "pickable" items are offered: purchasable, playable on Summoner's Rift,
 * not hidden from the shop, not champion-bound. Numeric JSON map keys ("3006")
 * arrive as PHP ints — every id is recast to string at the boundary. Image
 * resolution is name-keyed upstream, so a name collision degrades to null
 * (placeholder) rather than showing the wrong icon deterministically failing.
 */
final class ItemOptionsProjector
{
    public const TYPE = 'item';

    private const SUMMONERS_RIFT_MAP_ID = '11';

    /**
     * @param array<int|string, array<string, mixed>> $data   raw item.json "data" map (key = item id)
     * @param array<string, ?string>                  $images name-keyed ItemManager::getImages() result
     * @return list<array<string, mixed>>
     */
    public function project(array $data, array $images): array
    {
        $options = [];
        foreach ($data as $id => $entry) {
            if (!\is_array($entry) || !$this->isPickable($entry)) {
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

    /** @param array<string, mixed> $entry */
    private function isPickable(array $entry): bool
    {
        return ($entry['gold']['purchasable'] ?? false) === true
            && ($entry['maps'][self::SUMMONERS_RIFT_MAP_ID] ?? true) !== false
            && ($entry['hideFromAll'] ?? false) !== true
            && (string) ($entry['requiredChampion'] ?? '') === '';
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
