import type { FacetValue } from './facet-value';

/** A card's values keyed by facet; a facet the card has no value for is absent. */
export type CardValues = Readonly<Record<string, FacetValue>>;
