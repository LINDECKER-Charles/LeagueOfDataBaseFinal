import type { FacetSelection } from '../model/facet-selection';
import type { RangeSelection } from '../model/range-selection';

export function isRangeSelection(selection: FacetSelection): selection is RangeSelection {
  return typeof selection === 'object' && 'min' in selection;
}
