import type { Locale } from '../../i18n/locales';

/**
 * What the switcher changes. An absent field keeps what the URL says; `null` returns to
 * the default: the latest version, the locale's own Data Dragon language.
 */
export interface ContextTarget {
  readonly locale?: Locale;
  readonly version?: string | null;
  /** Data Dragon language, a regional variant such as `en_GB` (`?lang=`). */
  readonly lang?: string | null;
}
