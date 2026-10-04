<?php
declare(strict_types=1);

namespace LoDbParity;

use App\Service\API\DatasetRef;
use App\Service\API\SummonerManager;

/**
 * Every summoner spell of summoner.json, classic twins included, with the edition and the
 * twin the legacy manager gives it.
 */
final class SummonerProjection
{
    private readonly SummonerManager $manager;

    public function __construct(
        private readonly LegacyCatalog $catalog,
        private readonly DatasetRef $ref,
    ) {
        $this->manager = $catalog->summoners();
    }

    /** @return list<array<string, mixed>> */
    public function entries(): array
    {
        $data = $this->catalog->requireDataset($this->manager, $this->ref);

        $entries = [];
        foreach ($data['data'] ?? [] as $key => $entry) {
            if (is_array($entry)) {
                $entries[] = $this->spell((string) $key, $entry);
            }
        }

        return $entries;
    }

    /**
     * @param array<string, mixed> $entry
     * @return array<string, mixed>
     */
    private function spell(string $key, array $entry): array
    {
        $id   = (string) ($entry['id'] ?? $key);
        $twin = $this->manager->counterpart($key, $this->ref);

        return [
            'id'          => $id,
            'key'         => (string) ($entry['key'] ?? ''),
            'name'        => (string) ($entry['name'] ?? ''),
            'edition'     => $this->manager->editionOf($id, $entry)->value,
            'counterpart' => $twin === null
                ? null
                : ['id' => $twin['id'], 'edition' => $twin['edition']],
            'image'       => ImageStatus::of(
                $this->manager,
                $this->ref->version,
                $entry['image']['full'] ?? null,
            ),
        ];
    }
}
