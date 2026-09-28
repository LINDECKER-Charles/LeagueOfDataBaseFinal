import type { FacetDefinition } from '../model/facet-definition';
import type { FacetGroup } from '../model/facet-group';

/** The facets under their group heading, groups and facets in schema order. */
export function groupFacets(facets: readonly FacetDefinition[]): FacetGroup[] {
  const byGroup = new Map<string, FacetDefinition[]>();
  for (const facet of facets) {
    byGroup.set(facet.group, [...(byGroup.get(facet.group) ?? []), facet]);
  }
  return [...byGroup].map(([name, grouped]) => ({ name, facets: grouped }));
}
