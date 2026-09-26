import type { Locale } from '../i18n/locales';

/**
 * The context a page reads the catalogue in (ADR 0005): its locale, the version and the
 * Data Dragon language it shows. Resolved by the routing before the page renders, so a
 * page never guesses it.
 */
export interface PageContext {
  readonly locale: Locale;
  /** The version the page shows. */
  readonly version: string;
  /** Whether that version is an older one, named by the URL rather than implied. */
  readonly pinned: boolean;
  /** Data Dragon language of the content: the locale's own, or a regional variant. */
  readonly language: string;
}
