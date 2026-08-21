<?php
declare(strict_types=1);

namespace App\Service\API;

use App\Service\API\Concern\ResolvesEditionCounterpart;
use App\Service\API\Edition\Edition;
use App\Service\API\Edition\EditionAwareInterface;
use App\Service\API\Edition\ItemEditionRule;

final class ItemManager extends AbstractManager implements
    CategoriesInterface,
    EditionAwareInterface
{
    use ResolvesEditionCounterpart;

    public function type(): string
    {
        return 'item';
    }

    /** Items carry no edition field: the id range decides ({@see ItemEditionRule}). */
    public function editionOf(string $id, array $entry): Edition
    {
        return ItemEditionRule::of($id);
    }

    protected function counterpartId(string $id): ?string
    {
        return ItemEditionRule::counterpartId($id);
    }

    protected function imageUrl(string $version, string $name): string
    {
        return sprintf('%s/%s/img/item/%s', self::DDRAGON_CDN, $version, $name);
    }

    /**
     * Resolves a list of related item ids (item.into / item.from) into enriched
     * entries ready to link to their detail page. Ids missing from the current
     * dataset (items removed in a patch) are skipped, duplicates deduplicated,
     * and the input order (= recipe order) preserved.
     *
     * @param list<int|string> $ids
     * @return list<array{id: string, name: string, image: ?string, gold: ?int}>
     */
    public function resolveRelated(array $ids, string $version, string $lang): array
    {
        if ($ids === []) {
            return [];
        }

        $picked = $this->pickKnownItems($ids, $this->dataMap(new DatasetRef($version, $lang)));
        if ($picked === []) {
            return [];
        }

        // Secondary icons resolved through the ambient deferral scope: deferred
        // under a list render ({@see relatedIndex}), synchronous on detail / build
        // editor / trends (real icons on a cold version). The feature (name + link
        // + gold) does not depend on the image, so deferring never breaks it.
        $files = array_values(array_filter(array_map(
            static fn (array $entry): ?string => $entry['image']['full'] ?? null,
            $picked,
        )));
        $paths = $files === [] ? [] : $this->resolveImages($version, $files);

        $result = [];
        foreach ($picked as $id => $entry) {
            // PHP recasts numeric array keys to int — restore the string form.
            $result[] = $this->projectRelated((string) $id, $entry, $paths);
        }

        return $result;
    }

    /**
     * The requested ids that the dataset still carries, deduplicated, in input
     * (= recipe) order.
     *
     * @param list<int|string> $ids
     * @param array<mixed> $data id => item entry
     * @return array<string, array<string, mixed>>
     */
    private function pickKnownItems(array $ids, array $data): array
    {
        $picked = [];
        foreach ($ids as $id) {
            $id = (string) $id;
            if (isset($data[$id]) && !isset($picked[$id])) {
                $picked[$id] = $data[$id];
            }
        }

        return $picked;
    }

    /**
     * One related item as its consumers read it: enough to link to its detail
     * page and price it, with its icon when that one is already resolved.
     *
     * @param array<string, mixed> $entry
     * @param array<string, string> $paths image file name => cdn path
     * @return array{id: string, name: string, image: ?string, gold: ?int}
     */
    private function projectRelated(string $id, array $entry, array $paths): array
    {
        $file = $entry['image']['full'] ?? null;

        return [
            'id'    => $id,
            'name'  => (string) ($entry['name'] ?? ''),
            'image' => $file !== null ? ($paths[$file] ?? null) : null,
            // Total cost of the component/upgrade — lets the recipe tree nodes
            // show the price without an extra lookup.
            'gold'  => isset($entry['gold']['total']) ? (int) $entry['gold']['total'] : null,
        ];
    }

    /**
     * Index (id → resolved entry) of every upgrade (`into`) referenced by the
     * given items, resolved in a single pass. Lets the list show the real
     * name/icon/link for each upgrade id without a per-card resolution.
     *
     * @param iterable<array<string, mixed>> $items
     * @return array<string, array{id: string, name: string, image: ?string, gold: ?int}>
     */
    public function relatedIndex(iterable $items, string $version, string $lang): array
    {
        $ids = [];
        foreach ($items as $item) {
            foreach ($item['into'] ?? [] as $id) {
                $ids[(string) $id] = true;
            }
        }
        if ($ids === []) {
            return [];
        }

        // List render: the union of every item's evolution icons is a large cold
        // batch (one per upgrade target). Defer it like paginate() defers the
        // primary icons — otherwise a non-warm patch blocks the whole /objects
        // response on this synchronous batch (the switch-version-then-navigate lag).
        // Chips stay usable (name + link + gold); icons warm after the response.
        return $this->withImageDeferral(function () use ($ids, $version, $lang): array {
            $index = [];
            foreach ($this->resolveRelated(array_keys($ids), $version, $lang) as $entry) {
                $index[$entry['id']] = $entry;
            }

            return $index;
        });
    }

    /**
     * Top-down recipe tree: this item at the root, every component (`from`)
     * expanded recursively down to the base items ({@see RecipeTreeBuilder} for
     * the walk and its guards). Built from the cached dataset (no network egress
     * for the data); every icon of the tree is resolved in a single pass.
     *
     * @return array{
     *     id: string,
     *     name: string,
     *     image: ?string,
     *     gold: ?int,
     *     combine: ?int,
     *     children: list<mixed>
     * }|array{}
     */
    public function recipeTree(string $id, string $version, string $lang): array
    {
        $builder = new RecipeTreeBuilder($this->dataMap(new DatasetRef($version, $lang)));
        $tree    = $builder->build($id);
        if ($tree === null) {
            return [];
        }

        $files = $builder->referencedFiles();
        $paths = $files === [] ? [] : $this->resolveImages($version, $files);

        return $this->attachRecipeImages($tree, $paths);
    }

    /**
     * Replaces the raw icon file with its resolved URL across the whole tree.
     *
     * @param array<string, mixed> $node
     * @param array<string, ?string> $paths
     * @return array<string, mixed>
     */
    private function attachRecipeImages(array $node, array $paths): array
    {
        $node['image'] = $node['file'] !== null ? ($paths[$node['file']] ?? null) : null;
        unset($node['file']);
        $node['children'] = array_map(
            fn (array $child): array => $this->attachRecipeImages($child, $paths),
            $node['children'],
        );

        return $node;
    }

    /** Item entries carry no id field — their id is the map key, so only names match. */
    protected function matchesQuery(array $entry, string $needle): bool
    {
        $name = $entry['name'] ?? null;

        return is_scalar($name) && str_contains(mb_strtolower((string) $name), $needle);
    }

    /**
     * The item id lives in the map key; consumers expect it inside the entry.
     * The edition rides along so a hit list can tell the classic twin apart from
     * its current namesake.
     */
    protected function projectSearchResult(array $entry, string|int $key): array
    {
        return array_merge($entry, [
            'id'      => $key,
            'edition' => ItemEditionRule::of((string) $key)->value,
        ]);
    }

    /**
     * Keyed by item ID — the shape the item list/search/picker consumers index by.
     * Never by display name: since LoL Classic, "Faerie Charm" is both 1004 and
     * 771004, each with its own icon. The id comes from the map key (dataset
     * slices) or from the entry itself (search hits, see projectSearchResult).
     */
    protected function projectImages(array $data, array $resolved): array
    {
        $result = [];
        foreach ($data as $key => $entry) {
            $image = $entry['image']['full'] ?? null;
            if ($image === null || !($entry['name'] ?? null)) {
                continue;
            }
            $result[(string) ($entry['id'] ?? $key)] = $resolved[$image] ?? null;
        }

        return $result;
    }
}
