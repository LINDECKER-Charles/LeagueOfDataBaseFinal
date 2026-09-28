/** What the results column shows: the cards of the page and where the reader stands. */
export interface GridView<C> {
  readonly visible: readonly C[];
  /** Cards the criteria keep. */
  readonly matching: number;
  /** Cards of the whole list. */
  readonly total: number;
  /** One-based. */
  readonly page: number;
  /** At least one. */
  readonly pageCount: number;
  /**
   * The page cannot be drawn yet: the URL asks for a filtered or another page and only the
   * page the server rendered is known. The grid shows skeleton tiles meanwhile.
   */
  readonly isSkeleton: boolean;
}
