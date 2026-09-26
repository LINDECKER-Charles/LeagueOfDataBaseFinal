/** A cache of the worker: its name and how many entries it keeps. */
export interface CacheSpec {
  readonly name: string;
  readonly limit: number;
}
