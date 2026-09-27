import { PREFERENCES_COOKIE } from './preferences-cookie';
import { preferencesValue } from './preferences-value';
import type { Preferences } from './preferences';

// A year, as the legacy switcher's "remember" did.
const ONE_YEAR_IN_SECONDS = 365 * 24 * 60 * 60;
const EXPIRED = 0;

/**
 * `document.cookie` assignment that remembers `preferences`, or forgets them when null or
 * empty. Not HttpOnly: only the browser, and nginx for `/`, read it. Lax because nothing
 * cross-site needs it; Secure whenever the page is.
 */
export function preferencesCookieEntry(preferences: Preferences | null, secure: boolean): string {
  const value = preferencesValue(preferences);
  const lifetime = value === '' ? EXPIRED : ONE_YEAR_IN_SECONDS;
  const attributes = ['path=/', `max-age=${lifetime}`, 'samesite=lax'];
  return [`${PREFERENCES_COOKIE}=${value}`, ...attributes, ...(secure ? ['secure'] : [])].join(
    '; ',
  );
}
