import type { FacetState } from '../model/facet-state';
import type { RangeSelection } from '../model/range-selection';
import { withSelection } from './with-selection';

/** The state with a range set, or released when `range` is null. */
export function withRange(
  state: FacetState,
  key: string,
  range: RangeSelection | null,
): FacetState {
  return withSelection(state, key, range ?? undefined);
}
