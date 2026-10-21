/** One page refused since the server started, and how many times. */
export interface RefusedPageCount {
  /** `/{locale}/{version}/{page}`, as `refusedPagePathOf` writes it. */
  readonly path: string;
  /** Refusals since the server started, or since the path came back after an eviction. */
  readonly count: number;
}

/**
 * What `GET /refused?after={cursor}` answers on the metrics port: the pages whose count grew
 * after the cursor, oldest change first. Counts are cumulative, as a counter's: the API keeps
 * the last one it read per page and adds the increase, so that a second reader, a read made
 * twice or the API's restart never counts a refusal twice. No address, no client: a path and
 * a count.
 */
export interface RefusedPagesReport {
  /** Drawn at the server's start: another one means the counts started again from zero. */
  readonly instance: string;
  /** Where the next read starts (`?after=`). */
  readonly cursor: number;
  /** Whether every change after the cursor asked for is in this answer. */
  readonly isComplete: boolean;
  /** Paths dropped since the start to bound the memory, the least refused first. */
  readonly evicted: number;
  readonly pages: readonly RefusedPageCount[];
}
