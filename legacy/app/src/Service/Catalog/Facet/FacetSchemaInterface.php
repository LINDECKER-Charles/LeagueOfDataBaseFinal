<?php
declare(strict_types=1);

namespace App\Service\Catalog\Facet;

use App\Service\API\DatasetRef;
use Symfony\Component\DependencyInjection\Attribute\AutoconfigureTag;

/**
 * The filters of one resource list and the per-card values they match on —
 * the single owner of every normalisation a filter relies on (locale-stable
 * tokens, bucket thresholds, sentinel handling). Templates only echo
 * {@see valuesOf()} as `data-f-*` attributes; the island only combines them.
 */
#[AutoconfigureTag('app.facet_schema')]
interface FacetSchemaInterface
{
    /** DDragon resource type key ('champion', 'item', 'summoner', 'runesReforged'). */
    public function type(): string;

    /**
     * The facets of the list, in display order. Data-aware: option lists and
     * labels may be read off the dataset being rendered.
     *
     * @return list<FacetDefinition>
     */
    public function schema(DatasetRef $ref): array;

    /**
     * The facet values of one entry: choice tokens (string or list of strings),
     * a number for a range, true for a set toggle. A facet the entry has no
     * value for is simply absent.
     *
     * @param array<mixed> $entry the raw Data Dragon node (runes: augmented with
     *                            its `tree` key and `slot` index by the template)
     * @return array<string, string|list<string>|int|float|bool>
     */
    public function valuesOf(string $id, array $entry, DatasetRef $ref): array;
}
