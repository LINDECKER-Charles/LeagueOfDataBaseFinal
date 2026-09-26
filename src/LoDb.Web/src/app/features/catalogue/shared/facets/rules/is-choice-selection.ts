import type { ChoiceSelection } from '../model/choice-selection';
import type { FacetSelection } from '../model/facet-selection';

export function isChoiceSelection(selection: FacetSelection): selection is ChoiceSelection {
  return typeof selection === 'object' && 'values' in selection;
}
