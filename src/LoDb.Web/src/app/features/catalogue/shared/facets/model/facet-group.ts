import type { FacetDefinition } from './facet-definition';

/** The facets of a list under their heading, in schema order. */
export interface FacetGroup {
  readonly name: string;
  readonly facets: readonly FacetDefinition[];
}
