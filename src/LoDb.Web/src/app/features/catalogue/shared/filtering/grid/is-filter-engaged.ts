import type { FacetState } from '../../facets/model/facet-state';
import { activeFacetCount } from '../../facets/rules/active-facet-count';

/** Whether anything narrows the list: a search or an engaged facet. */
export function isFilterEngaged(query: string, facets: FacetState): boolean {
  return query.trim() !== '' || activeFacetCount(facets) > 0;
}
