import type { ChoiceSelection } from './choice-selection';
import type { RangeSelection } from './range-selection';

/** The engagement of one facet; `true` for a flag switched on. */
export type FacetSelection = ChoiceSelection | RangeSelection | true;
