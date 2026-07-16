<?php
declare(strict_types=1);

namespace App\Service\API;

use League\Flysystem\UnableToReadFile;

final class ChampionManager extends AbstractManager implements CategoriesInterface
{
    protected const TYPE = 'champion';

    protected function imageUrl(string $version, string $name): string
    {
        return sprintf('https://ddragon.leagueoflegends.com/cdn/%s/img/champion/%s', $version, $name);
    }

    /**
     * Full champion detail (spells, passive, skins, lore, tips) — a heavier
     * per-champion payload than {@see getByName()}'s summary. Cached in object
     * storage under its own key, fetched once through the gateway on a miss.
     *
     * @return array<mixed> the champion node, or [] when unavailable
     */
    public function getDetail(string $name, string $version, string $lang): array
    {
        $key = sprintf('data/%s/%s/championDetail/%s.json', $version, $lang, $name);

        try {
            $data = json_decode($this->ddragonStorage->read($key), true) ?? [];
        } catch (UnableToReadFile) {
            $url = sprintf(
                'https://ddragon.leagueoflegends.com/cdn/%s/data/%s/champion/%s.json',
                $version,
                $lang,
                $name
            );
            $data = json_decode($this->goFetcher->fetch($url), true) ?? [];
            $this->ddragonStorage->write($key, json_encode($data));
        }

        return $data['data'][$name] ?? [];
    }

    /**
     * Ingest the passive + spell icons of a detail payload (their DDragon paths
     * differ from champion portraits — {@code img/passive/…}, {@code img/spell/…}).
     *
     * @param array<mixed> $detail a {@see getDetail()} node
     * @return array<string,string> image.full => cdn path
     */
    public function getAbilityImages(array $detail, string $version): array
    {
        $urlsByName = [];

        if ($passive = $detail['passive']['image']['full'] ?? null) {
            $urlsByName[$passive] = sprintf(
                'https://ddragon.leagueoflegends.com/cdn/%s/img/passive/%s',
                $version,
                $passive
            );
        }

        foreach ($detail['spells'] ?? [] as $spell) {
            if ($full = $spell['image']['full'] ?? null) {
                $urlsByName[$full] = sprintf(
                    'https://ddragon.leagueoflegends.com/cdn/%s/img/spell/%s',
                    $version,
                    $full
                );
            }
        }

        return $urlsByName === [] ? [] : $this->resolveExternalImages($version, $urlsByName);
    }

    public function getByName(string $name, string $version, string $lang): array
    {
        $data = $this->getData($version, $lang)['data'] ?? [];
        if (isset($data[$name])) {
            return $data[$name];
        }

        throw new \RuntimeException(sprintf('Aucun champion trouvé avec l\'ID "%s".', $name));
    }

    public function searchByName(string $name, string $version, string $lang, int $max = 0): array
    {
        if (mb_strlen($name) < 2 || mb_strlen($name) > 50) {
            throw new \InvalidArgumentException('Nom invalide.');
        }

        $data = $this->getData($version, $lang)['data'] ?? [];
        if (!is_array($data)) {
            throw new \RuntimeException('Format de données invalide.');
        }

        $results = [];
        $search = mb_strtolower($name);
        foreach ($data as $champion) {
            if ($max !== 0 && count($results) >= $max) {
                break;
            }
            $idMatch = isset($champion['id']) && str_contains(mb_strtolower($champion['id']), $search);
            $nameMatch = isset($champion['name']) && str_contains(mb_strtolower($champion['name']), $search);
            if ($idMatch || $nameMatch) {
                $results[] = $champion;
            }
        }

        return $results;
    }

    protected function dataList(array $raw): array
    {
        return array_values($raw['data'] ?? []);
    }

    protected function imageEntries(array $data): array
    {
        $entries = [];
        foreach ($data as $d) {
            if (($name = $d['name'] ?? null) && ($img = $d['image']['full'] ?? null)) {
                $entries[$img] = $name;
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
            if (($d['name'] ?? null) && ($img = $d['image']['full'] ?? null)) {
                $result[] = $resolved[$img] ?? null;
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
        $json = $this->getData($version, $langue)['data'] ?? [];

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
