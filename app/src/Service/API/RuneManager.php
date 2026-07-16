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

    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array
    {
        if (!$data) {
            $data = array_values($this->getData($version, $lang) ?? []);
        }

        $names = [];
        foreach ($data as $d) {
            if ($d['icon'] ?? null) {
                $names[] = $d['icon'];
            }
            foreach ($d['slots'] ?? [] as $slot) {
                foreach ($slot['runes'] ?? [] as $rune) {
                    if ($rune['icon'] ?? null) {
                        $names[] = $rune['icon'];
                    }
                }
            }
        }
        $resolved = $this->resolveImages($version, $names, $force);

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

    public function paginate(string $version, string $langue, int $nb = 1, int $numPage = 1): array
    {
        $json = $this->getData($version, $langue);

        $ttSum = count($json);
        if ($nb === 0 || $nb > $ttSum) {
            $nb = $ttSum > 20 ? 20 : $ttSum;
        }
        $ttPage = (int) ceil($ttSum / max(1, $nb));
        if ($numPage > $ttPage) {
            $numPage = 1;
        }

        $json = $numPage <= 1
            ? $this->splitJson($nb, 0, $json)
            : $this->splitJson($nb, $nb * ($numPage - 1), $json);

        $images = $this->getImages($version, $langue, false, $json);

        return [
            static::TYPE.'s' => $json,
            'images' => $images,
            'meta' => [
                'currentPage' => $numPage,
                'nombrePage' => $ttPage,
                'itemPerPage' => $nb,
                'totalItem' => $ttSum,
                'type' => static::TYPE,
            ],
        ];
    }
}
