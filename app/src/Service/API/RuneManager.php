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

        throw new \RuntimeException(sprintf('Aucune rune trouvée avec le nom "%s".', $name));
    }

    protected function dataList(array $raw): array
    {
        return array_values($raw);
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

        $resolved = $this->resolveImages($version, array_keys($this->imageEntries($data)), $force);

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

    public function getImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''): string
    {
        return $this->resolveImage($version, $name, $force);
    }

    /** Les runes paginent la liste top-level des arbres, pas une map `['data']`. */
    protected function paginationCollection(array $raw): array
    {
        return $raw;
    }
}
