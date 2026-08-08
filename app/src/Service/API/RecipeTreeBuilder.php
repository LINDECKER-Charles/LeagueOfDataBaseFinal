<?php
declare(strict_types=1);

namespace App\Service\API;

/**
 * Builds the top-down recipe tree of one item from an already-loaded item map:
 * the item at the root, every component (`from`) expanded recursively down to
 * the base items. Pure walk — no storage, no egress — collecting on the way the
 * icon files the tree references, so the caller resolves them in a single pass
 * ({@see ItemManager::recipeTree}).
 *
 * Single-use, built per call: it carries the walk's collected state, not a
 * service's. The `seen` guard is per path, so it cuts cycles while still
 * allowing the same component in sibling branches (e.g. two long swords);
 * {@see MAX_DEPTH} additionally bounds pathological recipes.
 */
final class RecipeTreeBuilder
{
    /** Bounds pathological recipes (cycles are already cut by the per-path `seen` guard). */
    private const MAX_DEPTH = 6;

    /** @var array<string,true> icon file names referenced by the walked nodes */
    private array $files = [];

    /** @param array<mixed> $items id => item entry (a dataset's `data` map) */
    public function __construct(private readonly array $items) {}

    /**
     * @return array<string,mixed>|null null when the id is absent from the dataset
     */
    public function build(string $rootId): ?array
    {
        return $this->node($rootId, [], 0);
    }

    /**
     * Icon files referenced anywhere in the tree built so far.
     *
     * @return list<string>
     */
    public function referencedFiles(): array
    {
        return array_keys($this->files);
    }

    /**
     * @param array<string,true> $seen ancestors on the current path (cycle guard)
     * @return array<string,mixed>|null
     */
    private function node(string $id, array $seen, int $depth): ?array
    {
        if (!isset($this->items[$id]) || isset($seen[$id]) || $depth > self::MAX_DEPTH) {
            return null;
        }

        $seen[$id] = true;
        $entry     = $this->items[$id];
        $file      = $entry['image']['full'] ?? null;
        if ($file !== null) {
            $this->files[$file] = true;
        }

        return $this->describe($id, $entry, $file) + [
            'children' => $this->components($entry['from'] ?? [], $seen, $depth + 1),
        ];
    }

    /**
     * Flat shape of one node: what it is and what it costs, children excluded.
     * `file` is the raw icon name — the caller swaps it for its resolved URL.
     *
     * @param array<mixed> $entry
     * @return array{id: string, name: string, file: ?string, gold: ?int, combine: ?int}
     */
    private function describe(string $id, array $entry, ?string $file): array
    {
        return [
            'id'      => $id,
            'name'    => (string) ($entry['name'] ?? ''),
            'file'    => $file,
            'gold'    => isset($entry['gold']['total']) ? (int) $entry['gold']['total'] : null,
            'combine' => isset($entry['gold']['base']) ? (int) $entry['gold']['base'] : null,
        ];
    }

    /**
     * Expanded children of a node, in recipe order; ids the dataset no longer
     * carries (or that a guard cut) are dropped.
     *
     * @param array<mixed> $componentIds
     * @param array<string,true> $seen
     * @return list<array<string,mixed>>
     */
    private function components(array $componentIds, array $seen, int $depth): array
    {
        $children = [];
        foreach ($componentIds as $componentId) {
            $child = $this->node((string) $componentId, $seen, $depth);
            if ($child !== null) {
                $children[] = $child;
            }
        }

        return $children;
    }
}
