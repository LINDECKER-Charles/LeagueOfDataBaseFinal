/** A navigation as the audience sees it, once its address is normalized. */
export interface AudiencePage {
  /** What tells two views apart: path, language and version of the router URL (pageKeyOf). */
  readonly key: string;
  /** The address sent: the route's template, or the real path and the whitelisted query. */
  readonly url: string;
  /** Whether `url` is the real path: the document's title may then be sent too. */
  readonly isPublicPath: boolean;
  /** Status of an outcome rendered in place of the page (404, 5xx); null for a page. */
  readonly errorStatus: number | null;
  /** Locale of the URL, null outside the locales (`/b/…`). */
  readonly locale: string | null;
  /** The version named by the URL, or `current` when the page follows the latest one. */
  readonly version: string;
  /** Whether a list search is under way (`?q=`), whose text is never sent. */
  readonly isSearching: boolean;
}
