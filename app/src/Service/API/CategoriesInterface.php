<?php
declare(strict_types=1);

namespace App\Service\API;

/**
 * The searchable/browsable resources (champion, item, summoner): everything a
 * generic consumer needs to look one up, search it and render a page of it.
 * Runes deliberately stay out — their nested trees have no name search.
 */
interface CategoriesInterface
{
    /**
     * @return array<mixed>
     */
    public function getData(string $version, string $lang): array;

    /**
     * @return array<mixed>
     * @throws ResourceNotFoundException when no entry matches the id — a
     *                                   definitive upstream absence, i.e. a 404
     */
    public function getByName(string $name, string $version, string $lang): array;

    /**
     * Entries matching a free-text fragment.
     *
     * @param int $max maximum number of results to return (0 = unlimited)
     * @return list<array<mixed>>
     * @throws \InvalidArgumentException when the query is too short or over-long
     * @throws \RuntimeException when the underlying data cannot be fetched
     */
    public function searchByName(string $name, DatasetRef $dataset, int $max = 0): array;

    /**
     * Resolved image paths of a data slice ($data empty = the whole resource).
     * Champion, item and summoner maps are keyed by entry ID; runes keep their
     * nested tree shape. Never keyed by display name: LoL Classic twins share
     * it ("Faerie Charm" = 1004 and 771004).
     *
     * @param bool $force re-download even when the images are already stored
     * @param array<mixed> $data
     * @return array<mixed>
     */
    public function getImages(
        DatasetRef $dataset,
        bool $force = false,
        array $data = [],
    ): array;

    /**
     * Resolves the single image of a resource (detail pages) synchronously.
     *
     * @throws \RuntimeException when the image is unavailable upstream
     */
    public function getImage(string $version, string $name): string;

    /**
     * One page of the collection with its images and its navigation meta.
     *
     * @param int $perPage items per page; 0, or a value above the total, shows everything
     * @return array{
     *     images: array<mixed>,
     *     meta: array{
     *         currentPage: int,
     *         nombrePage: int,
     *         itemPerPage: int,
     *         totalItem: int,
     *         type: string
     *     }
     * } plus the slice itself under a `<type>s` key (e.g. `champions`)
     */
    public function paginate(DatasetRef $dataset, int $perPage = 1, int $page = 1): array;
}
