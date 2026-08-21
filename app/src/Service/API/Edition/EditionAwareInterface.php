<?php
declare(strict_types=1);

namespace App\Service\API\Edition;

use App\Service\API\DatasetRef;

/**
 * A resource that exists in both games (item, summoner): its entries carry an
 * edition and may have a twin in the other one. Champions and runes stay out —
 * LoL Classic ships neither through Data Dragon.
 */
interface EditionAwareInterface
{
    /** @param array<string, mixed> $entry the dataset node of $id */
    public function editionOf(string $id, array $entry): Edition;

    /** Edition of an id alone — for callers that only hold ids (prev/next pager). */
    public function editionOfId(string $id, DatasetRef $ref): Edition;

    /**
     * The other edition's twin of an entry, when the dataset carries it.
     *
     * @return ?array{id: string, name: string, edition: string}
     */
    public function counterpart(string $id, DatasetRef $ref): ?array;
}
