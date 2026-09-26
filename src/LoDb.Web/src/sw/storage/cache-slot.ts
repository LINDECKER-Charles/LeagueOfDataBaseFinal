/** An opened cache of the worker, with the number of entries it keeps. */
export interface CacheSlot {
  readonly cache: Cache;
  readonly limit: number;
}
