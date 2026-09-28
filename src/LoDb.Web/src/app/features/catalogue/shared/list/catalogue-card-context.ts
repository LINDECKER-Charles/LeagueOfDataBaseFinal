/** What a card template receives: the card, and its place on the page. */
export interface CatalogueCardContext<C> {
  readonly $implicit: C;
  readonly index: number;
}
