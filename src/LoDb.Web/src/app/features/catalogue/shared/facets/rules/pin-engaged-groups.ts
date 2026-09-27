import type { FacetGroup } from '../model/facet-group';
import type { FacetState } from '../model/facet-state';
import { countEngaged } from './count-engaged';

/**
 * The reader's folding of the groups with every engaged group the reader never folded pinned
 * open, or null when none needs pinning. Once pinned, the group no longer follows its
 * default, so clearing its last facet does not fold it under the pointer.
 */
export function pinEngagedGroups(
  groups: readonly FacetGroup[],
  state: FacetState,
  folds: Readonly<Record<string, boolean>>,
): Readonly<Record<string, boolean>> | null {
  const pinned = groups
    .filter((group) => folds[group.name] === undefined && countEngaged(group.facets, state) > 0)
    .map((group) => [group.name, true] as const);
  return pinned.length === 0 ? null : { ...folds, ...Object.fromEntries(pinned) };
}
