/**
 * Per-value result counts of the facets: how many cards each value would give while every
 * other engaged axis (and the search) still applies.
 */
export interface FacetCounts {
  /** Choice facets: value → count. */
  readonly options: Readonly<Record<string, ReadonlyMap<string, number>>>;
  /** Toggle facets: how many cards in context carry the flag. */
  readonly flagged: Readonly<Record<string, number>>;
}
