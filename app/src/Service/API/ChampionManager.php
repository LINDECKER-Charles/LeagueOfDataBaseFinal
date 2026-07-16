<?php
declare(strict_types=1);

namespace App\Service\API;

final class ChampionManager extends AbstractManager implements CategoriesInterface
{
    protected const TYPE = 'champion';

    protected function imageUrl(string $version, string $name): string
    {
        return sprintf('https://ddragon.leagueoflegends.com/cdn/%s/img/champion/%s', $version, $name);
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

    public function getImages(string $version, string $lang, bool $force = false, array $data = []): array
    {
        if (!$data) {
            $data = array_values($this->getData($version, $lang)['data'] ?? []);
        }

        $names = [];
        foreach ($data as $d) {
            if (($d['name'] ?? null) && ($img = $d['image']['full'] ?? null)) {
                $names[] = $img;
            }
        }
        $resolved = $this->resolveImages($version, $names, $force);

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
