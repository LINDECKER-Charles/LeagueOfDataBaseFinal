import type { Locale } from '../../i18n/locales';

/**
 * The context a visitor asked the switcher to remember (L3.9), or chose in this browsing
 * session. The values are only candidates: whoever applies them checks them first.
 */
export interface Preferences {
  /** Data Dragon language variant, such as `en_GB`, or null for the locale's own. */
  readonly lang: string | null;
  /** Version to read, or null to follow the latest. */
  readonly version: string | null;
  /** Interface locale chosen, which `/` opens on; absent when none was. */
  readonly locale?: Locale;
}
