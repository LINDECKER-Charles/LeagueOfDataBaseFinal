import type { RangeBounds } from './range-bounds';

/** What the cards of a list actually carry, per facet: nothing beyond it is offered. */
export interface FacetUniverse {
  /** Choice facets: the tokens present. */
  readonly present: Readonly<Record<string, ReadonlySet<string>>>;
  /** Range facets: the span of values present. */
  readonly bounds: Readonly<Record<string, RangeBounds>>;
  /** Toggle facets: how many cards are flagged. */
  readonly flagged: Readonly<Record<string, number>>;
}
