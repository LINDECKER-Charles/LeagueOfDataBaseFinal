import type { Locale } from '../i18n/locales';
import type { Preferences } from './preferences/preferences';

/** Where a page's context comes from, strongest first: path, query, remembered cookie. */
export interface ContextSources {
  readonly locale: Locale;
  /** Version segment of the path, or null when the URL follows the latest version. */
  readonly path: string | null;
  /** Query string of the page, with or without its `?` (`?version=`, `?lang=`). */
  readonly query: string;
  /**
   * What the browser remembers. Resolvers pass null, on the server as in the browser, so
   * the first client render matches the server one; the switcher applies it (L3.9).
   */
  readonly remembered: Preferences | null;
}
