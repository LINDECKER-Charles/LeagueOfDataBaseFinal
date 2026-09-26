import type { FacetState } from '../model/facet-state';
import { withSelection } from './with-selection';

/** The state with a flag switched on, or released. */
export function withToggle(state: FacetState, key: string, isOn: boolean): FacetState {
  return withSelection(state, key, isOn ? true : undefined);
}
