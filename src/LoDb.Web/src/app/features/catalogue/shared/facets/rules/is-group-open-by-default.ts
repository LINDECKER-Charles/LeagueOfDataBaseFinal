import type { FacetGroup } from '../model/facet-group';
import type { FacetState } from '../model/facet-state';
import { countEngaged } from './count-engaged';

/**
 * A group unfolds on its own when it holds a primary facet (a main axis of the list) or an
 * engaged one: a shared link must show what filters it.
 */
export function isGroupOpenByDefault(group: FacetGroup, state: FacetState): boolean {
  return group.facets.some((facet) => facet.primary) || countEngaged(group.facets, state) > 0;
}
