import type { FacetDefinition } from '../model/facet-definition';
import type { FacetState } from '../model/facet-state';

/** How many of these facets the state engages: the badge on a group heading. */
export function countEngaged(facets: readonly FacetDefinition[], state: FacetState): number {
  return facets.filter((facet) => state[facet.key] !== undefined).length;
}
