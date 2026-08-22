<?php
declare(strict_types=1);

namespace App\Service\Catalog\Facet;

/**
 * How a facet is operated and matched client-side. Values are the island's
 * vocabulary (assets/vue/filter/facets.ts) — a public contract.
 */
enum FacetKind: string
{
    /** A set of stable tokens per card; the reader picks one or several. */
    case Choice = 'choice';
    /** One number per card; the reader bounds it. Cards without it never match an active range. */
    case Range = 'range';
    /** A flag per card; the reader keeps only the flagged ones. */
    case Toggle = 'toggle';
}
