import type { UrlTree } from '@angular/router';

/** A neighbour or the hub of a pager: where it leads and what it is called. */
export interface PagerLink {
  /**
   * The page, locale prefix included: a UrlTree when the link carries a query or a fragment,
   * which a string would reach the router with escaped (`%3F`), or else a plain path.
   */
  readonly url: string | UrlTree;
  readonly name: string;
  /**
   * A chip after the name, with its tooltip: the edition of a neighbour whose name alone
   * repeats another entry's. Texts come in translated: the design system reads no catalogue.
   */
  readonly mark?: { readonly label: string; readonly hint: string };
}
