/** A neighbour or the hub of a pager: where it leads and what it is called. */
export interface PagerLink {
  /** Absolute path of the page, locale prefix included. */
  readonly url: string;
  readonly name: string;
}
