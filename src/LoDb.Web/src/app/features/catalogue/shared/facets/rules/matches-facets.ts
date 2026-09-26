import type { CardValues } from '../model/card-values';
import type { FacetDefinition } from '../model/facet-definition';
import type { FacetSelection } from '../model/facet-selection';
import type { FacetState } from '../model/facet-state';
import type { FacetValue } from '../model/facet-value';
import { isRangeSelection } from './is-range-selection';

function matchesFacet(value: FacetValue | undefined, selection: FacetSelection): boolean {
  if (selection === true) {
    return value === true;
  }
  if (isRangeSelection(selection)) {
    return typeof value === 'number' && value >= selection.min && value <= selection.max;
  }
  if (selection.values.length === 0) {
    return true;
  }
  if (!Array.isArray(value)) {
    return false;
  }
  const tokens: readonly string[] = value;
  return selection.all
    ? selection.values.every((wanted) => tokens.includes(wanted))
    : selection.values.some((wanted) => tokens.includes(wanted));
}

/**
 * Whether a card passes every engaged facet: facets AND together; the values of one choice
 * OR together, or AND in match-all mode; ranges are inclusive; a flag keeps flagged cards.
 * A facet the schema does not declare is ignored, whatever the state says.
 */
export function matchesFacets(
  values: CardValues,
  state: FacetState,
  schema: readonly FacetDefinition[],
): boolean {
  return schema.every((facet) => {
    const selection = state[facet.key];
    return selection === undefined || matchesFacet(values[facet.key], selection);
  });
}
