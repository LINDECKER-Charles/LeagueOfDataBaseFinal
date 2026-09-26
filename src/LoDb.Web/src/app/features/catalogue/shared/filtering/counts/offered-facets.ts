import type { FacetDefinition } from '../../facets/model/facet-definition';
import type { FacetUniverse } from './facet-universe';

function isOffered(facet: FacetDefinition, universe: FacetUniverse): boolean {
  switch (facet.kind) {
    case 'choice':
      return (universe.present[facet.key]?.size ?? 0) > 0;
    case 'range': {
      const bounds = universe.bounds[facet.key];
      return bounds !== undefined && bounds.min < bounds.max;
    }
    case 'toggle':
      return (universe.flagged[facet.key] ?? 0) > 0;
  }
}

/**
 * The facets the list can actually be narrowed by: a choice some card carries a value of, a
 * range whose values differ, a flag some card raises.
 */
export function offeredFacets(
  schema: readonly FacetDefinition[],
  universe: FacetUniverse,
): FacetDefinition[] {
  return schema.filter((facet) => isOffered(facet, universe));
}
