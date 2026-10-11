/**
 * What the browser tells of the reader's connection (Network Information API, Chromium
 * only): every field may be missing, as the whole object is in Firefox and Safari.
 */
export interface ConnectionHints {
  /** The reader asked to save data (`Save-Data`). */
  readonly saveData?: boolean;
  /** `slow-2g`, `2g`, `3g` or `4g`, from the measured round trips and throughput. */
  readonly effectiveType?: string;
}
