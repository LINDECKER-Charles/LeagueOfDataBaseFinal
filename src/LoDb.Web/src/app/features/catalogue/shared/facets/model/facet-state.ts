import type { FacetSelection } from './facet-selection';

/** The engaged facets of a list; an absent key filters nothing. */
export type FacetState = Readonly<Record<string, FacetSelection>>;
