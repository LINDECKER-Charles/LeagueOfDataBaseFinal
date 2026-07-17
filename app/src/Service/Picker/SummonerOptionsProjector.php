<?php
declare(strict_types=1);

namespace App\Service\Picker;

/**
 * Pure projection of the summoner-spell dataset into picker options.
 *
 * Only spells playable in CLASSIC (Summoner's Rift) are offered — ARAM/URF
 * exclusives would be noise in a favorite picker. Resolution stays
 * presence-based (unfiltered) so an already-stored favorite keeps rendering.
 */
final class SummonerOptionsProjector
{
    public const TYPE = 'summoner';

    private const REQUIRED_MODE = 'CLASSIC';

    /**
     * @param array<string, array<string, mixed>> $data   raw summoner.json "data" map (key = spell id)
     * @param array<string, ?string>              $images id-keyed SummonerManager::getImages() result
     * @return list<array{id: string, key: string, name: string, image: ?string}>
     */
    public function project(array $data, array $images): array
    {
        $options = [];
        foreach ($data as $storageKey => $entry) {
            if (!\in_array(self::REQUIRED_MODE, (array) ($entry['modes'] ?? []), true)) {
                continue;
            }
            $id = (string) ($entry['id'] ?? $storageKey);
            $options[] = [
                'id'    => $id,
                'key'   => (string) ($entry['key'] ?? ''),
                'name'  => (string) ($entry['name'] ?? $id),
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
            if ((string) ($entry['id'] ?? $storageKey) !== $id) {
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
}
