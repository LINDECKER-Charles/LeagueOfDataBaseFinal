/**
 * What the chrome carries on from the page it sits on (the legacy `page_selection`): its
 * catalogue links keep the pinned version and the language variant, and the switcher's chip
 * names the version the page reads.
 */
export interface NavSelection {
  /** The version the page reads, the latest included. */
  readonly shown: string;
  /** The version the catalogue links pin, null to follow the latest. */
  readonly version: string | null;
  /** The language variant they carry (`?lang=`), null for the locale's own. */
  readonly lang: string | null;
}
