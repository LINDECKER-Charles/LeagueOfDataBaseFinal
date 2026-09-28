<?php
declare(strict_types=1);

namespace LoDbParity;

use App\Service\API\DatasetRef;
use App\Service\API\Edition\ItemEditionRule;
use App\Service\API\ItemManager;
use App\Service\Catalog\Facet\Schema\ItemFacets;
use App\Stat\GameStat;

/**
 * Every item of item.json, classic twins included (the pickers leave them out), debris too:
 * `listed` says whether the browsable collection (listIndex(), cleaned of debris) keeps it,
 * and a listed item takes the name that collection shows.
 */
final class ItemProjection
{
    private readonly ItemManager $manager;

    private readonly ItemFacets $facets;

    public function __construct(
        private readonly LegacyCatalog $catalog,
        private readonly DatasetRef $ref,
    ) {
        $this->manager = $catalog->items();
        $this->facets  = new ItemFacets($this->manager, new NullTranslator());
    }

    /** @return list<array<string, mixed>> */
    public function entries(): array
    {
        $data   = $this->catalog->requireDataset($this->manager, $this->ref);
        $listed = $this->manager->listIndex($this->ref->version, $this->ref->lang);

        $entries = [];
        foreach ($data['data'] ?? [] as $key => $entry) {
            if (is_array($entry)) {
                $entries[] = $this->item((string) $key, $entry, $listed);
            }
        }

        return $entries;
    }

    /**
     * @param array<string, mixed> $entry
     * @param array<string, string> $listed browsable id => display name
     * @return array<string, mixed>
     */
    private function item(string $id, array $entry, array $listed): array
    {
        $values = $this->facets->valuesOf($id, $entry, $this->ref);
        $twin   = $this->manager->counterpart($id, $this->ref);

        return [
            'id'          => $id,
            'name'        => $listed[$id] ?? (string) ($entry['name'] ?? ''),
            'edition'     => ItemEditionRule::of($id)->value,
            'counterpart' => $twin === null
                ? null
                : ['id' => $twin['id'], 'edition' => $twin['edition']],
            'listed'      => array_key_exists($id, $listed),
            'tier'        => $values['tier'] ?? null,
            'stats'       => array_map(
                static fn (array $row): array => [
                    'stat'    => $row['stat']->value,
                    'value'   => $row['value'],
                    'percent' => $row['percent'],
                ],
                GameStat::fromItemStats($entry['stats'] ?? null),
            ),
            'image'       => ImageStatus::of(
                $this->manager,
                $this->ref->version,
                $entry['image']['full'] ?? null,
            ),
        ];
    }
}
