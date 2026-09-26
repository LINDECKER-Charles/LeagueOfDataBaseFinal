import type { Signal } from '@angular/core';
import type { ListStatus } from './list-status';
import type { PageSlice } from './page-slice';

/**
 * The list of a catalogue page as it arrives: first the page the URL names, rendered on the
 * server and readable by crawlers, then, in the browser, the whole list for instant
 * filtering. Built by injectCatalogueList; read by the page (heading, SEO, facets) and by
 * lodb-catalogue-list.
 */
export interface CatalogueListSource<L> {
  /** The page the server rendered, which the browser takes back from the transfer cache. */
  readonly firstPage: Signal<L | null>;
  /** Which page that is. */
  readonly slice: PageSlice;
  /** Page size of the list when its URL sets none. */
  readonly defaultSize: number;
  /** The whole list; in the browser only, null until it arrives. */
  readonly dataset: Signal<L | null>;
  /** The most complete list known: the whole one, else the first page. */
  readonly list: Signal<L | null>;
  readonly status: Signal<ListStatus>;
}
