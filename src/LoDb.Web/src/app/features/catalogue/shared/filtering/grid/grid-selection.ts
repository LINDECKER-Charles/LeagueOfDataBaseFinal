/** The cards the criteria keep, and the page of them shown. */
export interface GridSelection<T> {
  readonly matching: readonly T[];
  /** At least one, even when nothing matches. */
  readonly pageCount: number;
  /** The requested page, sent back to 1 when it fell past the last one. */
  readonly page: number;
  readonly visible: readonly T[];
}
