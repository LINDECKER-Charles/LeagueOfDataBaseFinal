<?php
declare(strict_types=1);

namespace App\Service\API\Concern;

use App\Service\API\DatasetRef;
use App\Service\API\Edition\Edition;

/**
 * Edition twin lookup shared by the resources that exist in both games (item,
 * summoner): "1004 / Faerie Charm (current)" ↔ "771004 / Faerie Charm (classic)".
 * Each resource brings its own rule ({@see editionOf}, {@see counterpartId});
 * this trait checks the candidate against the dataset so a detail page can
 * cross-link the twin without ever linking to an id the patch does not carry.
 *
 * The twin must also carry the SAME display name: Riot reused item ids over
 * the years, so 773001 (classic "Abyssal Scepter") strips to 3001, which is a
 * different item today. A twin is the homonym the reader may confuse this
 * entry with — a namesake, or nothing.
 *
 * Composed onto {@see \App\Service\API\AbstractManager} subclasses: reads the
 * id-keyed `data` map through {@see dataMap}.
 */
trait ResolvesEditionCounterpart
{
    /** @return array<mixed> */
    abstract protected function dataMap(DatasetRef $ref): array;

    /** @param array<string, mixed> $entry */
    abstract public function editionOf(string $id, array $entry): Edition;

    /** Id the other edition's twin would carry, null when none can exist. */
    abstract protected function counterpartId(string $id): ?string;

    /** An id absent from the dataset is read as current-game: the safe default. */
    public function editionOfId(string $id, DatasetRef $ref): Edition
    {
        $entry = $this->dataMap($ref)[$id] ?? null;

        return \is_array($entry) ? $this->editionOf($id, $entry) : Edition::Modern;
    }

    /**
     * The other edition's twin of an entry, when the dataset carries it.
     *
     * @return ?array{id: string, name: string, edition: string}
     */
    public function counterpart(string $id, DatasetRef $ref): ?array
    {
        $twinId = $this->counterpartId($id);
        if ($twinId === null) {
            return null;
        }

        $data  = $this->dataMap($ref);
        $twin  = $data[$twinId] ?? null;
        $entry = $data[$id] ?? null;
        if (!\is_array($twin) || !\is_array($entry) || !$this->isNamesake($entry, $twin)) {
            return null;
        }

        return [
            'id'      => $twinId,
            'name'    => (string) ($twin['name'] ?? $twinId),
            'edition' => $this->editionOf($twinId, $twin)->value,
        ];
    }

    /**
     * @param array<string, mixed> $entry
     * @param array<string, mixed> $twin
     */
    private function isNamesake(array $entry, array $twin): bool
    {
        $name = $entry['name'] ?? null;

        return \is_string($name) && $name !== '' && $name === ($twin['name'] ?? null);
    }
}
