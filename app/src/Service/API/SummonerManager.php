<?php
declare(strict_types=1);

namespace App\Service\API;

final class SummonerManager extends AbstractManager implements CategoriesInterface
{
    protected const TYPE = 'summoner';

    protected function imageUrl(string $version, string $name): string
    {
        return sprintf('https://ddragon.leagueoflegends.com/cdn/%s/img/spell/%s', $version, $name);
    }

    public function getByName(string $name, string $version, string $lang): array
    {
        $data = $this->getData($version, $lang)['data'] ?? [];
        foreach ($data as $d) {
            if (isset($d['id']) && $d['id'] === $name) {
                return $d;
            }
        }

        throw ResourceNotFoundException::forEntry(static::TYPE, $name);
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
        foreach ($data as $summoner) {
            if ($max !== 0 && count($results) >= $max) {
                break;
            }
            $idMatch = isset($summoner['id']) && str_contains(mb_strtolower($summoner['id']), $search);
            $nameMatch = isset($summoner['name']) && str_contains(mb_strtolower($summoner['name']), $search);
            if ($idMatch || $nameMatch) {
                $results[] = $summoner;
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
            if (($id = $d['id'] ?? null) && ($img = $d['image']['full'] ?? null)) {
                $entries[$img] = $d['name'] ?? $id;
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
            $id = $d['id'] ?? null;
            $img = $d['image']['full'] ?? null;
            if ($id && $img) {
                $result[$id] = $resolved[$img] ?? null;
            }
        }

        return $result;
    }

    public function getImage(string $name, string $version, array $dir = [], bool $force = false, string $lang = ''): string
    {
        return $this->resolveImage($version, $name, $force);
    }

    /** Les sorts d'invocateur sont peu nombreux (~16) et affichés en entier — pas de plafond. */
    protected function perPageCap(): int
    {
        return PHP_INT_MAX;
    }
}
