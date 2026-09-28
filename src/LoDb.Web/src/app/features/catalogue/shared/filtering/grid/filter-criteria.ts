import type { FacetDefinition } from '../../facets/model/facet-definition';
import type { FacetState } from '../../facets/model/facet-state';

/** What narrows the list: the search text and the engaged facets, read by the schema. */
export interface FilterCriteria {
  readonly query: string;
  readonly facets: FacetState;
  readonly schema: readonly FacetDefinition[];
}
