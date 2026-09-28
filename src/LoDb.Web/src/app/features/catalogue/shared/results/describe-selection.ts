import type { FacetDefinition } from '../facets/model/facet-definition';
import type { FacetSelection } from '../facets/model/facet-selection';
import { isRangeSelection } from '../facets/rules/is-range-selection';

const ANY_JOINER = ', ';
const ALL_JOINER = ' + ';

/** The chip of an engaged facet above the results: "Role: Mage, Tank", "Price 0–3000 g". */
export function describeSelection(facet: FacetDefinition, selection: FacetSelection): string {
  if (selection === true) {
    return facet.label;
  }
  if (isRangeSelection(selection)) {
    const unit = facet.unit === null ? '' : ` ${facet.unit}`;
    return `${facet.label} ${selection.min}–${selection.max}${unit}`;
  }
  const labels = new Map(facet.options.map((option) => [option.value, option.label]));
  const values = selection.values.map((value) => labels.get(value) ?? value);
  return `${facet.label}: ${values.join(selection.all ? ALL_JOINER : ANY_JOINER)}`;
}
