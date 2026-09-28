/** What `/api/meta` and, on a detail, the API's answer say about a catalogue page. */
export interface CanonicalFacts {
  /** The version the short URLs show; null until the first ingestion completes. */
  readonly latest: string | null;
  /** Every version Data Dragon lists. */
  readonly versions: readonly string[];
  /**
   * `canonicalPath` of the detail (`items/1036-long-sword`), null when the API holds no such
   * entity, undefined while it has not been asked (a list, or a detail before its fetch).
   */
  readonly canonicalPath?: string | null;
}
