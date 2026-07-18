<?php
declare(strict_types=1);

namespace App\Service\API;

final class RuneManager extends AbstractManager
{
    protected const TYPE = 'runesReforged';

    protected function imageUrl(string $version, string $name): string
    {
        // Rune icons live under a version-less path, and $name is already a full sub-path.
        return 'https://ddragon.leagueoflegends.com/cdn/img/'.$name;
    }

    public function getByName(string $name, string $version, string $lang): array
    {
        $data = $this->getData($version, $lang);
        foreach ($data as $rune) {
            if (($rune['key'] ?? null) === $name) {
                return $rune;
            }
        }

        throw ResourceNotFoundException::forEntry(static::TYPE, $name);
    }

    /**
     * Rune trees are nested: the tree icon plus every keystone/minor rune icon,
     * each mapped to its display name.
     */
    protected function imageEntries(array $data): array
    {
        $entries = [];
        foreach ($data as $d) {
            if ($icon = $d['icon'] ?? null) {
                $entries[$icon] = $d['name'] ?? $icon;
            }
            foreach ($d['slots'] ?? [] as $slot) {
                foreach ($slot['runes'] ?? [] as $rune) {
                    if ($icon = $rune['icon'] ?? null) {
                        $entries[$icon] = $rune['name'] ?? $icon;
                    }
                }
            }
        }

        return $entries;
    }

    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array
    {
        if (!$data) {
            $data = $this->dataList($this->getData($version, $lang));
        }

        return $this->mapTreeImages($data, $this->resolveImages($version, array_keys($this->imageEntries($data)), $force));
    }

    /**
     * Detail page: resolve one tree's nested icons **synchronously**. Unlike the
     * list ({@see getImages}), a detail render must never defer image ingestion —
     * on a cold version that would paint broken icons until the next visit
     * (mirrors {@see AbstractManager::resolveImage} for the flat resources).
     *
     * @param array<mixed> $tree
     * @return array<string, mixed>
     */
    public function getDetailImages(string $version, array $tree, bool $force = false): array
    {
        $resolved = $this->resolveImages($version, array_keys($this->imageEntries([$tree])), $force, allowDefer: false);

        return $this->mapTreeImages([$tree], $resolved);
    }

    /**
     * Map resolved icon paths back onto the nested tree structure the template
     * consumes: `treeKey => {icon, slots[slotIndex][runeKey]}`.
     *
     * @param array<mixed> $data
     * @param array<string, string> $resolved image name => cdn path
     * @return array<string, mixed>
     */
    private function mapTreeImages(array $data, array $resolved): array
    {
        $result = [];
        foreach ($data as $d) {
            $key = $d['key'] ?? null;
            $icon = $d['icon'] ?? null;
            if (!$key || !$icon) {
                continue;
            }
            $result[$key]['icon'] = $resolved[$icon] ?? null;

            foreach ($d['slots'] ?? [] as $index => $slot) {
                foreach ($slot['runes'] ?? [] as $rune) {
                    $runeIcon = $rune['icon'] ?? null;
                    $runeKey = $rune['key'] ?? null;
                    if (!$runeIcon || !$runeKey) {
                        continue;
                    }
                    $result[$key]['slots'][$index][$runeKey] = $resolved[$runeIcon] ?? null;
                }
            }
        }

        return $result;
    }

    /** Les runes paginent la liste top-level des arbres, pas une map `['data']`. */
    protected function paginationCollection(array $raw): array
    {
        return $raw;
    }

    /** La route détail des runes est indexée par la KEY d'arbre, pas par l'id numérique. */
    protected function entryRouteId(array $entry, string $storageKey): string
    {
        return (string) ($entry['key'] ?? $storageKey);
    }
}
