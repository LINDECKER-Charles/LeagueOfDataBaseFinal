import { defineFacet } from '../facets/define-facet';
import type { FacetDefinition } from '../facets/model/facet-definition';

/** A facet definition for specs: its key and kind, labelled by its key. */
export function facetOf(
  partial: Partial<FacetDefinition> & Pick<FacetDefinition, 'key' | 'kind'>,
): FacetDefinition {
  return defineFacet({ label: partial.key, group: 'g', ...partial });
}
