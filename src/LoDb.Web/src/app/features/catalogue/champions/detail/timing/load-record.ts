/** How the entity of a detail page arrived, noted by withLoadTiming in the browser. */
export interface LoadRecord {
  /** The first page of the visit, whose times the Navigation Timing entry holds. */
  readonly initial: boolean;
  /** `performance.now()` when the navigation asked for the entity. */
  readonly startedAt: number;
  /** Time the entity took to arrive, in milliseconds. */
  readonly fetchMs: number;
}
