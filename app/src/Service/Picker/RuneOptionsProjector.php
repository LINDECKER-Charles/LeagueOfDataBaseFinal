<?php
declare(strict_types=1);

namespace App\Service\Picker;

/**
 * Pure projection of the runesReforged dataset into picker trees.
 *
 * Output preserves DDragon's canonical shape and order: 5 trees, each with its
 * 4 slots as a list of perk lists. shortDesc is stripped of DDragon's inline
 * markup server-side so the island never has to render trusted-ish HTML.
 * Favorite resolution accepts the numeric id of a perk OR of a whole tree.
 */
final class RuneOptionsProjector
{
    public const TYPE = 'rune';

    /**
     * @param list<array<string, mixed>>                                              $data   top-level runesReforged.json tree list
     * @param array<string, array{icon?: ?string, slots?: array<int, array<string, ?string>>}> $images nested RuneManager::getImages() result
     * @return list<array<string, mixed>>
     */
    public function project(array $data, array $images): array
    {
        $trees = [];
        foreach ($data as $tree) {
            if (!\is_array($tree)) {
                continue;
            }
            $treeKey = (string) ($tree['key'] ?? '');
            $treeImages = $images[$treeKey] ?? [];
            $trees[] = [
                'id'    => (int) ($tree['id'] ?? 0),
                'key'   => $treeKey,
                'name'  => (string) ($tree['name'] ?? $treeKey),
                'icon'  => PickerFormat::imagePath($treeImages['icon'] ?? null),
                'slots' => $this->projectSlots($tree, (array) ($treeImages['slots'] ?? [])),
            ];
        }

        return $trees;
    }

    /**
     * @param list<array<string, mixed>> $data
     * @param array<string, array{icon?: ?string, slots?: array<int, array<string, ?string>>}> $images
     * @return ?array{id: string, name: string, image: ?string, type: string}
     */
    public function resolve(array $data, array $images, string $id): ?array
    {
        foreach ($this->project($data, $images) as $tree) {
            $found = $this->findInTree($tree, $id);
            if ($found !== null) {
                return $found;
            }
        }

        return null;
    }

    /**
     * @param array<string, mixed>                  $tree
     * @param array<int, array<string, ?string>> $slotIcons
     * @return list<list<array{id: int, key: string, name: string, icon: ?string, shortDesc: string}>>
     */
    private function projectSlots(array $tree, array $slotIcons): array
    {
        $slots = [];
        foreach (array_values((array) ($tree['slots'] ?? [])) as $index => $slot) {
            $perks = [];
            foreach ((array) ($slot['runes'] ?? []) as $perk) {
                $perks[] = $this->projectPerk((array) $perk, (array) ($slotIcons[$index] ?? []));
            }
            $slots[] = $perks;
        }

        return $slots;
    }

    /**
     * @param array<string, mixed>    $perk
     * @param array<string, ?string> $icons perk-key-keyed icon paths of the perk's slot
     * @return array{id: int, key: string, name: string, icon: ?string, shortDesc: string}
     */
    private function projectPerk(array $perk, array $icons): array
    {
        $perkKey = (string) ($perk['key'] ?? '');

        return [
            'id'        => (int) ($perk['id'] ?? 0),
            'key'       => $perkKey,
            'name'      => (string) ($perk['name'] ?? $perkKey),
            'icon'      => PickerFormat::imagePath($icons[$perkKey] ?? null),
            'shortDesc' => strip_tags((string) ($perk['shortDesc'] ?? '')),
        ];
    }

    /**
     * @param array<string, mixed> $tree a {@see project()} tree
     * @return ?array{id: string, name: string, image: ?string, type: string}
     */
    private function findInTree(array $tree, string $id): ?array
    {
        if ((string) $tree['id'] === $id) {
            return ['id' => $id, 'name' => $tree['name'], 'image' => $tree['icon'], 'type' => self::TYPE];
        }

        foreach ($tree['slots'] as $perks) {
            foreach ($perks as $perk) {
                if ((string) $perk['id'] === $id) {
                    return ['id' => $id, 'name' => $perk['name'], 'image' => $perk['icon'], 'type' => self::TYPE];
                }
            }
        }

        return null;
    }
}
