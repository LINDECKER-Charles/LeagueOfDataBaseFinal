/** What the load-time badge shows, in milliseconds; null for a time nobody measured. */
export interface LoadTimes {
  readonly serverMs: number | null;
  readonly clientMs: number;
}
