import type { FacetSelection } from '../model/facet-selection';
import type { FacetState } from '../model/facet-state';

/** The state with one facet set, or dropped when `selection` is undefined. */
export function withSelection(
  state: FacetState,
  key: string,
  selection: FacetSelection | undefined,
): FacetState {
  const next: Record<string, FacetSelection> = { ...state };
  if (selection === undefined) {
    delete next[key];
  } else {
    next[key] = selection;
  }
  return next;
}
