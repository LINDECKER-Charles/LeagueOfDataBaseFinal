import type { UrlTree } from '@angular/router';
import type { NavEntry } from '../shell/nav-entry';

/** A destination of the chrome, with the address it points to from the current page. */
export interface ChromeLink {
  readonly entry: NavEntry;
  /**
   * A tree rather than a string: a variant's `?lang=` then reaches the router as a query,
   * where a string holding `?` would be escaped into the path (jalon 4, G3).
   */
  readonly target: UrlTree;
}
