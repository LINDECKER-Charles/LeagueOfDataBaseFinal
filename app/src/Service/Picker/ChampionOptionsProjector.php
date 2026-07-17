<?php
declare(strict_types=1);

namespace App\Service\Picker;

/**
 * Pure projection of the champion dataset into picker options.
 *
 * ChampionManager::getImages() returns a POSITIONAL list holding one path (or
 * null) per data entry that carries both a name and an image file, in dataset
 * order. {@see imagesById()} realigns it with the exact same skip rule, so
 * options stay correct even when an entry lacks its image node.
 */
final class ChampionOptionsProjector
{
    public const TYPE = 'champion';

    /**
     * @param array<string, array<string, mixed>> $data   raw champion.json "data" map (key = champion id)
     * @param list<?string>                       $images positional ChampionManager::getImages() result
     * @return list<array{id: string, key: string, name: string, image: ?string}>
     */
    public function project(array $data, array $images): array
    {
        $imagesById = $this->imagesById($data, $images);

        $options = [];
        foreach ($data as $storageKey => $entry) {
            $id = (string) ($entry['id'] ?? $storageKey);
            $options[] = [
                'id'    => $id,
                'key'   => (string) ($entry['key'] ?? ''),
                'name'  => (string) ($entry['name'] ?? $storageKey),
                'image' => $imagesById[$id] ?? null,
            ];
        }

        return PickerFormat::sortByName($options);
    }

    /**
     * @param array<string, array<string, mixed>> $data
     * @param list<?string>                       $images
     * @return ?array{id: string, name: string, image: ?string, type: string}
     */
    public function resolve(array $data, array $images, string $id): ?array
    {
        foreach ($data as $storageKey => $entry) {
            if ((string) ($entry['id'] ?? $storageKey) !== $id) {
                continue;
            }

            return [
                'id'    => $id,
                'name'  => (string) ($entry['name'] ?? $id),
                'image' => $this->imagesById($data, $images)[$id] ?? null,
                'type'  => self::TYPE,
            ];
        }

        return null;
    }

    /**
     * Realign the positional image list on champion ids, consuming one slot per
     * entry that has both a name and an image file — the manager's own rule.
     *
     * @param array<string, array<string, mixed>> $data
     * @param list<?string>                       $images
     * @return array<string, ?string>
     */
    private function imagesById(array $data, array $images): array
    {
        $byId = [];
        $cursor = 0;
        foreach ($data as $storageKey => $entry) {
            $hasImage = ($entry['name'] ?? null) && ($entry['image']['full'] ?? null);
            $byId[(string) ($entry['id'] ?? $storageKey)] = $hasImage
                ? PickerFormat::imagePath($images[$cursor++] ?? null)
                : null;
        }

        return $byId;
    }
}
