<?php
declare(strict_types=1);

namespace App\Service\API\Concern;

use App\Service\API\DatasetRef;
use App\Service\API\ResourceNotFoundException;

/**
 * Entry lookup shared by the DDragon resource managers: the single-entry
 * resolution every detail route goes through, and the free-text search behind
 * the search APIs. Split out of {@see AbstractManager} so "how an entry is
 * found" stays separable from dataset/image/manifest concerns.
 *
 * Composed onto {@see AbstractManager}; walks the collection exposed by
 * {@see PaginatesResources::paginationCollection} (the `['data']` map for
 * champion/item/summoner, the top-level list for runes). Resources diverge on
 * three seams only: how an entry is located from its route id
 * ({@see findEntry}), what a query matches ({@see matchesQuery}) and what a hit
 * looks like ({@see projectSearchResult}).
 */
trait ResolvesEntries
{
    /** Bounds of a free-text {@see searchByName} query (guards against 1-char floods / abuse). */
    private const NAME_MIN = 2;
    private const NAME_MAX = 50;

    /**
     * The entry a detail route points at.
     *
     * @return array<mixed>
     * @throws ResourceNotFoundException when no entry matches the id — a
     *                                   definitive upstream absence, i.e. a 404
     */
    public function getByName(string $name, string $version, string $lang): array
    {
        $collection = $this->paginationCollection(
            $this->dataset(new DatasetRef($version, $lang))
        );

        return $this->findEntry($collection, $name)
            ?? throw ResourceNotFoundException::forEntry($this->type(), $name);
    }

    /**
     * Entries matching a free-text fragment, in collection order.
     *
     * @param int $max maximum number of results to return (0 = unlimited)
     * @return list<array<mixed>>
     * @throws \InvalidArgumentException when the query is too short or over-long
     */
    public function searchByName(string $name, DatasetRef $dataset, int $max = 0): array
    {
        $this->assertSearchable($name);

        $needle  = mb_strtolower($name);
        $results = [];
        foreach ($this->paginationCollection($this->dataset($dataset)) as $key => $entry) {
            if ($max !== 0 && count($results) >= $max) {
                break;
            }
            if (is_array($entry) && $this->matchesQuery($entry, $needle)) {
                $results[] = $this->projectSearchResult($entry, $key);
            }
        }

        return $results;
    }

    /**
     * Locate one entry in the collection. Default: a direct hit on the map key,
     * which *is* the resource id for champion/item/summoner; runes override it.
     *
     * @param array<mixed> $collection
     * @return ?array<mixed>
     */
    protected function findEntry(array $collection, string $name): ?array
    {
        $entry = $collection[$name] ?? null;

        return is_array($entry) ? $entry : null;
    }

    /**
     * Does an entry match an already-lowercased query? Default: its id or its
     * display name contains the fragment.
     *
     * @param array<mixed> $entry
     */
    protected function matchesQuery(array $entry, string $needle): bool
    {
        foreach (['id', 'name'] as $field) {
            $value = $entry[$field] ?? null;
            if (is_scalar($value) && str_contains(mb_strtolower((string) $value), $needle)) {
                return true;
            }
        }

        return false;
    }

    /**
     * Shape of one search hit. Default: the entry as stored — resources whose id
     * lives in the map key rather than in the entry override this to inject it.
     *
     * @param array<mixed> $entry
     * @return array<mixed>
     */
    protected function projectSearchResult(array $entry, string|int $key): array
    {
        return $entry;
    }

    /** @throws \InvalidArgumentException when a free-text query is too short or over-long */
    protected function assertSearchable(string $name): void
    {
        if (mb_strlen($name) < self::NAME_MIN || mb_strlen($name) > self::NAME_MAX) {
            throw new \InvalidArgumentException('Nom invalide.');
        }
    }
}
