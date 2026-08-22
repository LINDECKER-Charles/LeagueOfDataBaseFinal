<?php
declare(strict_types=1);

namespace App\Service\Catalog\Facet;

/**
 * One filter of a list page, as the island receives it ({@see toArray()}).
 * Values are never carried here — they ride on the cards as `data-f-<key>`
 * attributes ({@see FacetSchemaInterface::valuesOf()}); the definition only
 * says how to present and combine them. `key` doubles as the URL parameter,
 * so it must stay stable and locale-independent.
 *
 * A plain value object assembled by named arguments: the argument count is
 * the shape of the contract, not a sign of a method doing several things.
 *
 * @phpstan-type Option array{value: string, label: string}
 */
final class FacetDefinition
{
    /**
     * @param list<Option> $options known values with their display labels, in
     *                              display order — the island only shows the
     *                              ones actually present in the grid
     * @param bool $isPrimary shown inline above the grid (the others sit in
     *                        the advanced panel)
     * @param bool $isMultiple choice facets: several values (OR) vs one
     * @param bool $canMatchAll choice facets: offer the any/all switch
     * @param ?string $unit range facets: display suffix
     * @param float $step range facets: slider granularity
     */
    public function __construct(
        public readonly string $key,
        public readonly FacetKind $kind,
        public readonly string $label,
        public readonly string $group,
        public readonly array $options = [],
        public readonly bool $isPrimary = false,
        public readonly bool $isMultiple = true,
        public readonly bool $canMatchAll = false,
        public readonly ?string $unit = null,
        public readonly float $step = 1.0,
    ) {}

    /** @return array<string,mixed> */
    public function toArray(): array
    {
        return [
            'key'      => $this->key,
            'kind'     => $this->kind->value,
            'label'    => $this->label,
            'group'    => $this->group,
            'options'  => $this->options,
            'primary'  => $this->isPrimary,
            'multiple' => $this->isMultiple,
            'matchAll' => $this->canMatchAll,
            'unit'     => $this->unit,
            'step'     => $this->step,
        ];
    }
}
