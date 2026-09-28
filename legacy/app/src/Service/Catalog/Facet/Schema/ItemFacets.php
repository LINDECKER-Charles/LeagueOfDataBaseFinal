<?php
declare(strict_types=1);

namespace App\Service\Catalog\Facet\Schema;

use App\Service\API\DatasetRef;
use App\Service\API\Edition\Edition;
use App\Service\API\Edition\ItemEditionRule;
use App\Service\API\ItemManager;
use App\Service\Catalog\Facet\FacetDefinition;
use App\Service\Catalog\Facet\FacetKind;
use App\Service\Catalog\Facet\FacetSchemaInterface;
use App\Service\Catalog\GameMap;
use App\Service\Tools\DdragonText;
use App\Stat\GameStat;
use Symfony\Contracts\Translation\TranslatorInterface;

/**
 * Item list filters. Knowledge reused, never restated: map ids ({@see GameMap}),
 * stat columns and their percent encoding ({@see GameStat}), the edition rule
 * ({@see ItemEditionRule}). What is decided here:
 *  - tier is structural, not `depth` alone — 514 depth-less items are mostly
 *    consumables, Arena augments and quest pseudo-items, not components;
 *  - a stat facet only ever sees items that carry the stat (an empty stats
 *    block yields no value, so "AD ≥ 0" does not match every trinket);
 *  - percent stats are stored as fractions (0.25) and exposed as 25 (%).
 */
final class ItemFacets implements FacetSchemaInterface
{
    private const TIER_COMPONENT = 'component';
    private const TIER_EPIC = 'epic';
    private const TIER_LEGENDARY = 'legendary';
    private const EPIC_DEPTH = 2;
    private const PERCENT_SUFFIX = '_pct';
    private const PERCENT_UNIT = '%';
    private const PERCENT_SCALE = 100;

    public function __construct(
        private readonly ItemManager $items,
        private readonly TranslatorInterface $translator,
    ) {}

    public function type(): string
    {
        return 'item';
    }

    public function schema(DatasetRef $ref): array
    {
        return [
            new FacetDefinition(
                key: 'tag',
                kind: FacetKind::Choice,
                label: $this->label('tag'),
                group: $this->group('identity'),
                options: $this->tagOptions($ref),
                isPrimary: true,
                canMatchAll: true,
            ),
            new FacetDefinition(
                key: 'edition',
                kind: FacetKind::Choice,
                label: $this->label('edition'),
                group: $this->group('identity'),
                options: $this->editionOptions(),
                isPrimary: true,
                isMultiple: false,
            ),
            new FacetDefinition(
                key: 'map',
                kind: FacetKind::Choice,
                label: $this->label('map'),
                group: $this->group('availability'),
                options: $this->mapOptions(),
            ),
            new FacetDefinition(
                key: 'tier',
                kind: FacetKind::Choice,
                label: $this->label('tier'),
                group: $this->group('identity'),
                options: $this->tierOptions(),
            ),
            new FacetDefinition(
                key: 'purchasable',
                kind: FacetKind::Toggle,
                label: $this->label('purchasable'),
                group: $this->group('availability'),
            ),
            new FacetDefinition(
                key: 'consumable',
                kind: FacetKind::Toggle,
                label: $this->label('consumable'),
                group: $this->group('availability'),
            ),
            new FacetDefinition(
                key: 'price',
                kind: FacetKind::Range,
                label: $this->label('price'),
                group: $this->group('economy'),
            ),
            ...array_map(
                fn (array $column): FacetDefinition => $this->stat($column['stat'], $column['percent']),
                GameStat::itemStatColumns(),
            ),
        ];
    }

    public function valuesOf(string $id, array $entry, DatasetRef $ref): array
    {
        $values = [
            'tag'     => array_values(array_map(strval(...), $entry['tags'] ?? [])),
            'edition' => ItemEditionRule::of($id)->value,
            'map'     => array_map(static fn (GameMap $map): string => $map->value, GameMap::availableOn($entry)),
            'price'   => (int) ($entry['gold']['total'] ?? 0),
        ];
        if (($tier = self::tierOf($entry)) !== null) {
            $values['tier'] = $tier;
        }
        if (($entry['gold']['purchasable'] ?? false) === true) {
            $values['purchasable'] = true;
        }
        if (($entry['consumed'] ?? false) === true) {
            $values['consumable'] = true;
        }
        foreach (GameStat::fromItemStats($entry['stats'] ?? null) as $row) {
            $values[self::statKey($row['stat'], $row['percent'])] = $row['percent']
                ? (int) round($row['value'] * self::PERCENT_SCALE)
                : $row['value'];
        }

        return $values;
    }

    /** @param array<mixed> $entry */
    private static function tierOf(array $entry): ?string
    {
        $depth = $entry['depth'] ?? null;
        if ($depth === null) {
            return ($entry['into'] ?? []) !== [] ? self::TIER_COMPONENT : null;
        }

        return (int) $depth <= self::EPIC_DEPTH ? self::TIER_EPIC : self::TIER_LEGENDARY;
    }

    private static function statKey(GameStat $stat, bool $isPercent): string
    {
        return $stat->value.($isPercent ? self::PERCENT_SUFFIX : '');
    }

    /**
     * Every category tag of the patch, humanised like the card chips.
     *
     * @return list<array{value: string, label: string}>
     */
    private function tagOptions(DatasetRef $ref): array
    {
        $tags = [];
        foreach ($this->items->getData($ref->version, $ref->lang)['data'] ?? [] as $entry) {
            foreach ($entry['tags'] ?? [] as $tag) {
                $tags[(string) $tag] = DdragonText::tagLabel((string) $tag);
            }
        }
        asort($tags);

        return array_map(
            static fn (string $value, string $label): array => ['value' => $value, 'label' => $label],
            array_keys($tags),
            $tags,
        );
    }

    /** @return list<array{value: string, label: string}> */
    private function editionOptions(): array
    {
        return array_map(
            fn (Edition $edition): array => [
                'value' => $edition->value,
                'label' => $this->translator->trans('edition.'.$edition->value),
            ],
            Edition::cases(),
        );
    }

    /** @return list<array{value: string, label: string}> */
    private function mapOptions(): array
    {
        return array_map(
            fn (GameMap $map): array => [
                'value' => $map->value,
                'label' => $this->translator->trans($map->labelKey()),
            ],
            GameMap::cases(),
        );
    }

    /** @return list<array{value: string, label: string}> */
    private function tierOptions(): array
    {
        return array_map(
            fn (string $tier): array => [
                'value' => $tier,
                'label' => $this->translator->trans('facet.item.tiers.'.$tier),
            ],
            [self::TIER_COMPONENT, self::TIER_EPIC, self::TIER_LEGENDARY],
        );
    }

    private function stat(GameStat $stat, bool $isPercent): FacetDefinition
    {
        return new FacetDefinition(
            key: self::statKey($stat, $isPercent),
            kind: FacetKind::Range,
            label: $this->translator->trans($stat->labelKey()),
            group: $this->group('stats'),
            unit: $isPercent ? self::PERCENT_UNIT : null,
        );
    }

    private function label(string $key): string
    {
        return $this->translator->trans('facet.item.'.$key);
    }

    private function group(string $name): string
    {
        return $this->translator->trans('facet.group.'.$name);
    }
}
