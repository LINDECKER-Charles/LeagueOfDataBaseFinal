/** The page of the list the server renders: the one the URL names, unfiltered. */
export interface PageSlice {
  /** One-based. */
  readonly page: number;
  /** PAGE_SIZE_ALL or a positive page size. */
  readonly size: number;
}
