import type { FilterUrlSpec } from './filter-url-spec';
import type { FilterUrlState } from './filter-url-state';

/** How FilterUrlSync reads a list's state, and hands it back what a navigation carried. */
export interface FilterUrlBinding {
  readonly spec: () => FilterUrlSpec;
  readonly current: () => FilterUrlState;
  /** A navigation brought another state (a link, the history): the list adopts it. */
  readonly adopt: (state: FilterUrlState) => void;
}
