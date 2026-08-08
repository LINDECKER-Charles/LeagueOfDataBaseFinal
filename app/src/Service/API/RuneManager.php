<?php
declare(strict_types=1);

namespace App\Service\API;

final class RuneManager extends AbstractManager
{
    public function type(): string
    {
        return 'runesReforged';
    }

    protected function imageUrl(string $version, string $name): string
    {
        // Rune icons live under a version-less path, and $name is already a full sub-path.
        return self::DDRAGON_CDN.'/img/'.$name;
    }

    /** Rune trees are a top-level list: the route id is the tree `key`, not a map key. */
    protected function findEntry(array $collection, string $name): ?array
    {
        foreach ($collection as $tree) {
            if (is_array($tree) && ($tree['key'] ?? null) === $name) {
                return $tree;
            }
        }

        return null;
    }

    /**
     * Rune trees are nested: the tree icon plus every keystone/minor rune icon,
     * each mapped to its display name.
     */
    protected function imageEntries(array $data): array
    {
        $entries = [];
        foreach ($data as $tree) {
            if ($icon = $tree['icon'] ?? null) {
                $entries[$icon] = $tree['name'] ?? $icon;
            }
            $entries = array_replace($entries, $this->runeEntries($tree['slots'] ?? []));
        }

        return $entries;
    }

    /**
     * Every keystone/minor rune icon of one tree's slots, mapped to its name.
     *
     * @param array<mixed> $slots
     * @return array<string,string> icon path => rune display name
     */
    private function runeEntries(array $slots): array
    {
        $entries = [];
        foreach ($slots as $slot) {
            foreach ($slot['runes'] ?? [] as $rune) {
                if ($icon = $rune['icon'] ?? null) {
                    $entries[$icon] = $rune['name'] ?? $icon;
                }
            }
        }

        return $entries;
    }

    /**
     * Nested shape the rune templates consume: `treeKey => {icon,
     * slots[slotIndex][runeKey]}` — the tree structure, not a flat list.
     */
    protected function projectImages(array $data, array $resolved): array
    {
        $result = [];
        foreach ($data as $tree) {
            $key  = $tree['key'] ?? null;
            $icon = $tree['icon'] ?? null;
            if (!$key || !$icon) {
                continue;
            }
            $result[$key]['icon'] = $resolved[$icon] ?? null;

            // Kept off the tree when a patch carries no usable rune slot, so the
            // shape stays exactly what the templates already branch on.
            $slots = $this->mapSlotImages($tree['slots'] ?? [], $resolved);
            if ($slots !== []) {
                $result[$key]['slots'] = $slots;
            }
        }

        return $result;
    }

    /**
     * @param array<mixed> $slots
     * @param array<string,string> $resolved image name => cdn path
     * @return array<int|string, array<string,?string>> slotIndex => runeKey => cdn path
     */
    private function mapSlotImages(array $slots, array $resolved): array
    {
        $mapped = [];
        foreach ($slots as $index => $slot) {
            foreach ($slot['runes'] ?? [] as $rune) {
                $icon = $rune['icon'] ?? null;
                $key  = $rune['key'] ?? null;
                if ($icon && $key) {
                    $mapped[$index][$key] = $resolved[$icon] ?? null;
                }
            }
        }

        return $mapped;
    }

    /** Runes paginate the top-level list of trees, not a `['data']` map. */
    protected function paginationCollection(array $raw): array
    {
        return $raw;
    }

    /** The rune detail route is keyed by the tree KEY, not by the numeric id. */
    protected function entryRouteId(array $entry, string $storageKey): string
    {
        return (string) ($entry['key'] ?? $storageKey);
    }
}
