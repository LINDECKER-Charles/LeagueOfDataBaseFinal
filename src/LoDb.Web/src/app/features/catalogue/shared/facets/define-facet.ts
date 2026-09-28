import type { FacetDefinition } from './model/facet-definition';

/**
 * A facet definition from its key, kind, label and group, every other field at its
 * default: several values, no any/all switch, folded, no unit, a step of one.
 */
export function defineFacet(
  facet: Partial<FacetDefinition> & Pick<FacetDefinition, 'key' | 'kind' | 'label' | 'group'>,
): FacetDefinition {
  return {
    options: [],
    primary: false,
    multiple: true,
    matchAll: false,
    unit: null,
    step: 1,
    ...facet,
  };
}
