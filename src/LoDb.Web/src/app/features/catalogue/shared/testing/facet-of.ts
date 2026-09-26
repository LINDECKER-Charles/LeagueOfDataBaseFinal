import type { FacetDefinition } from '../facets/model/facet-definition';

/** A facet definition for specs: the key and the kind, every other field at its default. */
export function facetOf(
  partial: Partial<FacetDefinition> & Pick<FacetDefinition, 'key' | 'kind'>,
): FacetDefinition {
  return {
    label: partial.key,
    group: 'g',
    options: [],
    primary: false,
    multiple: true,
    matchAll: false,
    unit: null,
    step: 1,
    ...partial,
  };
}
