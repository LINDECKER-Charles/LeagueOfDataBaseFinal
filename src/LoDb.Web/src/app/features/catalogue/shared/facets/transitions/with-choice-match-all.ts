import type { FacetState } from '../model/facet-state';
import { isChoiceSelection } from '../rules/is-choice-selection';
import { withSelection } from './with-selection';

/** The state with the any/all mode of an engaged choice switched; otherwise unchanged. */
export function withChoiceMatchAll(state: FacetState, key: string, all: boolean): FacetState {
  const current = state[key];
  if (current === undefined || !isChoiceSelection(current)) {
    return state;
  }
  return withSelection(state, key, { ...current, all });
}
