/** What every catalogue list shares: its entries (one page, or all of them) and its total. */
export interface CatalogueListLike<C> {
  readonly entries: readonly C[];
  /** Entries of the whole list, whatever the page. */
  readonly total: number;
}
