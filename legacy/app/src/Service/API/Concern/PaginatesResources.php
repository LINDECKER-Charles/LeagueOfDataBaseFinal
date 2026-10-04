<?php
declare(strict_types=1);

namespace App\Service\API\Concern;

use App\Service\API\DatasetRef;

/**
 * Collection navigation shared by the DDragon resource managers: server-side
 * pagination, the ordered prev/next index, and the seams resources diverge on
 * (paginated root, per-page cap, detail route id). Split out of
 * {@see AbstractManager} so dataset/image/manifest concerns stay separable from
 * how a collection is sliced and indexed.
 *
 * Composed onto {@see AbstractManager}: image resolution of a slice comes from
 * its sibling {@see ResolvesImages}.
 */
trait PaginatesResources
{
    /** @return array<mixed> */
    abstract protected function dataset(DatasetRef $ref): array;

    /**
     * @param string[] $names
     * @return array{images: array<string,?string>, pending: list<string>}
     */
    abstract public function manifestStatus(string $version, array $names): array;

    /** DDragon resource type key ('champion', 'item', 'summoner', 'runesReforged'). */
    abstract public function type(): string;

    /**
     * Generic pagination (list slice + images + meta) shared by the four
     * resources. Only two things vary: the paginated root
     * ({@see paginationCollection}: the `['data']` map for champion/item/summoner,
     * a top-level list for runes) and the per-page cap ({@see perPageCap}). The
     * slice keeps the original keys (resource id) — {@see slicePage} slices in
     * key-preserving mode.
     *
     * `pending` lists the image names a cold render deferred (refreshable
     * placeholders), distinct from a null image (settled absence).
     *
     * @return array<string,mixed> {<type>s, images, pending, meta}
     */
    public function paginate(DatasetRef $dataset, int $perPage = 1, int $page = 1): array
    {
        $collection = $this->paginationCollection($this->dataset($dataset));
        $totalItems = count($collection);
        $perPage    = $this->normalizePerPage($perPage, $totalItems);
        $pageCount  = (int) ceil($totalItems / max(1, $perPage));
        // An out-of-range page is not an error here: fall back to the first one.
        $page       = $page > $pageCount ? 1 : $page;

        $slice  = $this->slicePage($perPage, $page <= 1 ? 0 : $perPage * ($page - 1), $collection);
        // Images first: a synchronous resolution settles the manifest this
        // request reads `pending` from.
        $images = $this->sliceImages($dataset, $slice);

        // The type-suffixed collection key and the `meta` key names are a public
        // contract the list controllers and templates read verbatim — renaming
        // them (or the stray french `nombrePage`) is a cross-cutting change.
        return [
            $this->type().'s' => $slice,
            'images' => $images,
            'pending' => $this->pendingImageNames($dataset->version, $slice),
            'meta' => [
                'currentPage' => $page,
                'nombrePage' => $pageCount,
                'itemPerPage' => $perPage,
                'totalItem' => $totalItems,
                'type' => $this->type(),
            ],
        ];
    }

    /**
     * Images of a list slice. The list/preview render is the one place cold
     * images may defer: it shows placeholders and warms them after the response
     * (or via the SSE loader). Every other getImages caller resolves inline
     * (safe default).
     *
     * @param array<mixed> $slice
     * @return array<mixed>
     */
    private function sliceImages(DatasetRef $dataset, array $slice): array
    {
        return $this->withImageDeferral(
            fn (): array => $this->getImages($dataset, false, $slice)
        );
    }

    /**
     * The shape of {@see paginate()} with nothing in it — what a failed preview
     * renders instead of taking the page down. Derived here so the template
     * contract (`<type>s`, images, pending, meta) can never drift from it.
     *
     * @return array<string,mixed>
     */
    public function emptyPage(): array
    {
        return [$this->type().'s' => [], 'images' => [], 'pending' => [], 'meta' => []];
    }

    /**
     * Image names of a slice the manifest does not settle yet — deferred by a
     * cold list render, and refreshed in page once they land. Keyed by name
     * so a template can test membership in O(1).
     *
     * @param array<mixed> $slice
     * @return array<string,true>
     */
    private function pendingImageNames(string $version, array $slice): array
    {
        $names = array_keys($this->imageEntries($slice));

        return array_fill_keys($this->manifestStatus($version, $names)['pending'], true);
    }

    /** 0 ("show all") — or a request above the total — collapses to the capped whole. */
    private function normalizePerPage(int $perPage, int $totalItems): int
    {
        return $perPage === 0 || $perPage > $totalItems
            ? min($this->perPageCap(), $totalItems)
            : $perPage;
    }

    /**
     * Dataset root to paginate. Defaults to the `['data']` map (key = resource
     * id); overridden by resources whose JSON is a top-level list (runes).
     *
     * @param array<mixed> $raw
     * @return array<mixed>
     */
    protected function paginationCollection(array $raw): array
    {
        return $raw['data'] ?? [];
    }

    /**
     * List form of {@see paginationCollection} (values, re-indexed) — the single
     * hook for "which root to iterate" (the 'data' map, or runes' top-level list).
     *
     * @param array<mixed> $raw
     * @return array<mixed>
     */
    protected function dataList(array $raw): array
    {
        return array_values($this->paginationCollection($raw));
    }

    /**
     * Ordered route-id => display-name index of the whole collection — backs the
     * previous/next navigation on detail pages without resolving any image.
     * Order is the collection's own (the same one the list pages render).
     *
     * @return array<string,string>
     */
    public function listIndex(string $version, string $lang): array
    {
        $dataset = new DatasetRef($version, $lang);
        $index   = [];
        foreach ($this->paginationCollection($this->dataset($dataset)) as $key => $entry) {
            if (\is_array($entry)) {
                $index[$this->entryRouteId($entry, (string) $key)] =
                    (string) ($entry['name'] ?? $key);
            }
        }

        return $index;
    }

    /** Identifier used by the detail route for one collection entry (map key by default). */
    protected function entryRouteId(array $entry, string $storageKey): string
    {
        return (string) ($entry['id'] ?? $storageKey);
    }

    /**
     * Cap when the caller passes perPage=0 ("show all"). Unbounded by default: the
     * four resources render their whole list (client-side filtering; runes have ~5
     * trees). A future bulky resource can tighten this by overriding.
     */
    protected function perPageCap(): int
    {
        return PHP_INT_MAX;
    }

    /**
     * Key-preserving page slice of a collection.
     *
     * @param array<mixed> $collection
     * @return array<mixed>
     */
    protected final function slicePage(int $perPage, int $offset, array $collection): array
    {
        return array_slice($collection, $offset, $perPage, true);
    }
}
