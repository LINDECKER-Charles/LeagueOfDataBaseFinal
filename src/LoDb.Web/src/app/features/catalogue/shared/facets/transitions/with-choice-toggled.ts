import type { ChoiceSelection } from '../model/choice-selection';
import type { FacetDefinition } from '../model/facet-definition';
import type { FacetState } from '../model/facet-state';
import { isChoiceSelection } from '../rules/is-choice-selection';
import { withSelection } from './with-selection';

const NO_CHOICE: ChoiceSelection = { values: [], all: false };

/**
 * The state with one value of a choice flipped: a single-choice facet replaces its value
 * instead of accumulating, and a choice left empty is dropped.
 */
export function withChoiceToggled(
  state: FacetState,
  facet: FacetDefinition,
  value: string,
): FacetState {
  const current = state[facet.key];
  const selection = current !== undefined && isChoiceSelection(current) ? current : NO_CHOICE;
  const isSelected = selection.values.includes(value);
  const kept = selection.values.filter((selected) => selected !== value);
  const values = isSelected ? kept : facet.multiple ? [...selection.values, value] : [value];
  return withSelection(state, facet.key, values.length > 0 ? { ...selection, values } : undefined);
}
