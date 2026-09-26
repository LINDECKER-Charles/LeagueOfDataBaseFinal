/** The entries on either side of a detail page in its list's order; null at either end. */
export interface Neighbours<C> {
  readonly previous: C | null;
  readonly next: C | null;
}
