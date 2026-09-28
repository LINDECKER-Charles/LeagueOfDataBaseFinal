<?php
declare(strict_types=1);

namespace App\Service\Picker;

/**
 * Pure projection of the champion dataset into picker options.
 *
 * ChampionManager::getImages() is keyed by champion id (an entry without a
 * name or an image node is simply absent), so the projection is a plain
 * per-id lookup — no positional realignment to keep in sync with the manager.
 */
final class ChampionOptionsProjector
{
    public const TYPE = 'champion';

    /**
     * @param array<string, array<string, mixed>> $data raw champion.json "data" map
     *        (key = champion id)
     * @param array<string, ?string> $images id-keyed ChampionManager::getImages() result
     * @return list<array{id: string, key: string, name: string, image: ?string}>
     */
    public function project(array $data, array $images): array
    {
        $options = [];
        foreach ($data as $storageKey => $entry) {
            $id = $this->entryId($storageKey, $entry);
            $options[] = [
                'id'    => $id,
                'key'   => (string) ($entry['key'] ?? ''),
                'name'  => (string) ($entry['name'] ?? $storageKey),
                'image' => PickerFormat::imagePath($images[$id] ?? null),
            ];
        }

        return PickerFormat::sortByName($options);
    }

    /**
     * @param array<string, array<string, mixed>> $data
     * @param array<string, ?string>              $images
     * @return ?array{id: string, name: string, image: ?string, type: string}
     */
    public function resolve(array $data, array $images, string $id): ?array
    {
        foreach ($data as $storageKey => $entry) {
            if ($this->entryId($storageKey, $entry) !== $id) {
                continue;
            }

            return [
                'id'    => $id,
                'name'  => (string) ($entry['name'] ?? $id),
                'image' => PickerFormat::imagePath($images[$id] ?? null),
                'type'  => self::TYPE,
            ];
        }

        return null;
    }

    /**
     * Identity of a dataset entry: its own id, falling back to the storage key
     * the champion is filed under.
     *
     * @param array<string, mixed> $entry
     */
    private function entryId(int|string $storageKey, array $entry): string
    {
        return (string) ($entry['id'] ?? $storageKey);
    }
}
