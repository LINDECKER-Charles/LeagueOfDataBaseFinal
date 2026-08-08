<?php
declare(strict_types=1);

namespace App\Service\API;

final class SummonerManager extends AbstractManager implements CategoriesInterface
{
    public function type(): string
    {
        return 'summoner';
    }

    protected function imageUrl(string $version, string $name): string
    {
        return sprintf('%s/%s/img/spell/%s', self::DDRAGON_CDN, $version, $name);
    }

    protected function imageEntries(array $data): array
    {
        $entries = [];
        foreach ($data as $entry) {
            if (($id = $entry['id'] ?? null) && ($image = $entry['image']['full'] ?? null)) {
                $entries[$image] = $entry['name'] ?? $id;
            }
        }

        return $entries;
    }

    /** Keyed by spell id ("SummonerFlash") — the shape the summoner views index by. */
    protected function projectImages(array $data, array $resolved): array
    {
        $result = [];
        foreach ($data as $entry) {
            $id    = $entry['id'] ?? null;
            $image = $entry['image']['full'] ?? null;
            if ($id && $image) {
                $result[$id] = $resolved[$image] ?? null;
            }
        }

        return $result;
    }
}
