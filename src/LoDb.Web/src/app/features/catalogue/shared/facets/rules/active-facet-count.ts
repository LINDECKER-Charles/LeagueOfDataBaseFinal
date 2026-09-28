import type { FacetState } from '../model/facet-state';

/**
 * How many facets the state engages, whatever the number of values picked: the badge of the
 * console and of the mobile trigger, which is therefore the sum of the group badges.
 */
export function activeFacetCount(state: FacetState): number {
  return Object.keys(state).length;
}
