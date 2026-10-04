<?php
declare(strict_types=1);

namespace App\Service\API;

use App\Service\API\Concern\ResolvesEditionCounterpart;
use App\Service\API\Edition\Edition;
use App\Service\API\Edition\EditionAwareInterface;
use App\Service\API\Edition\SummonerEditionRule;

final class SummonerManager extends AbstractManager implements
    CategoriesInterface,
    EditionAwareInterface
{
    use ResolvesEditionCounterpart;

    public function type(): string
    {
        return 'summoner';
    }

    /** The `modes` list decides ({@see SummonerEditionRule}); the id is not consulted. */
    public function editionOf(string $id, array $entry): Edition
    {
        return SummonerEditionRule::of($entry);
    }

    protected function counterpartId(string $id): ?string
    {
        return SummonerEditionRule::counterpartId($id);
    }

    /** A hit carries its edition so "Flash" (current) and "Flash" (classic) stay apart. */
    protected function projectSearchResult(array $entry, string|int $key): array
    {
        return $entry + ['edition' => SummonerEditionRule::of($entry)->value];
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
}
