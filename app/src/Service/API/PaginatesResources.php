<?php
declare(strict_types=1);

namespace App\Service\API;

/**
 * Collection navigation shared by the DDragon resource managers: server-side
 * pagination, the ordered prev/next index, and the seams resources diverge on
 * (paginated root, per-page cap, detail route id). Split out of
 * {@see AbstractManager} so dataset/image/manifest concerns stay separable from
 * how a collection is sliced and indexed.
 *
 * Composed onto {@see AbstractManager}; relies on its data/image access
 * (`getData`, `getImages`, `static::TYPE`).
 */
trait PaginatesResources
{
    /**
     * Pagination générique (tranche de liste + images + méta) partagée par les
     * quatre ressources. Seuls varient la racine paginée ({@see paginationCollection} :
     * map `['data']` pour champion/item/summoner, liste top-level pour les runes) et
     * le plafond par page ({@see perPageCap}). La tranche préserve les clés d'origine
     * (id de ressource) — {@see splitJson} slice en mode préservation de clés.
     *
     * @return array<string,mixed> {<type>s, images, meta}
     */
    public function paginate(string $version, string $lang, int $nb = 1, int $numPage = 1): array
    {
        $json = $this->paginationCollection($this->getData($version, $lang));

        $ttSum = count($json);
        if ($nb === 0 || $nb > $ttSum) {
            $nb = min($this->perPageCap(), $ttSum);
        }
        $ttPage = (int) ceil($ttSum / max(1, $nb));
        if ($numPage > $ttPage) {
            $numPage = 1;
        }

        $json = $numPage <= 1
            ? $this->splitJson($nb, 0, $json)
            : $this->splitJson($nb, $nb * ($numPage - 1), $json);

        $images = $this->getImages($version, $lang, false, $json);

        return [
            static::TYPE.'s' => $json,
            'images' => $images,
            'meta' => [
                'currentPage' => $numPage,
                'nombrePage' => $ttPage,
                'itemPerPage' => $nb,
                'totalItem' => $ttSum,
                'type' => static::TYPE,
            ],
        ];
    }

    /**
     * Racine du dataset à paginer. Par défaut la map `['data']` (clé = id de
     * ressource) ; surchargée par les ressources dont le JSON est une liste
     * top-level (runes).
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
        $index = [];
        foreach ($this->paginationCollection($this->getData($version, $lang)) as $key => $entry) {
            if (\is_array($entry)) {
                $index[$this->entryRouteId($entry, (string) $key)] = (string) ($entry['name'] ?? $key);
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
     * Cap when the caller passes nb=0 ("show all"). Unbounded by default: the four
     * resources render their whole list (client-side filtering; runes have ~5
     * trees). A future bulky resource can tighten this by overriding.
     */
    protected function perPageCap(): int
    {
        return PHP_INT_MAX;
    }

    /**
     * @param array<mixed> $json
     * @return array<mixed>
     */
    protected final function splitJson(int $nb, int $start, array $json): array
    {
        return array_slice($json, $start, $nb, true);
    }
}
