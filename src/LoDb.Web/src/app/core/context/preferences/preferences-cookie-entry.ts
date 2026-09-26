import { PREFERENCES_COOKIE } from './preferences-cookie';
import { PREFERENCES_FIELDS } from './preferences-fields';
import type { Preferences } from './preferences';

// Remembered for a year, the lifetime the cookie policy announces for lod_prefs.
const ONE_YEAR_IN_SECONDS = 365 * 24 * 60 * 60;
const EXPIRED = 0;

// A null field is left out: it means "the default", which needs no remembering.
function valueOf(preferences: Preferences | null): string {
  const fields = new URLSearchParams();
  if (preferences?.lang) {
    fields.set(PREFERENCES_FIELDS.lang, preferences.lang);
  }
  if (preferences?.version) {
    fields.set(PREFERENCES_FIELDS.version, preferences.version);
  }
  return fields.toString();
}

/**
 * `document.cookie` assignment that remembers `preferences`, or forgets them when null or
 * empty. Not HttpOnly: only the browser reads it. Lax because nothing cross-site needs it;
 * Secure whenever the page is.
 */
export function preferencesCookieEntry(preferences: Preferences | null, secure: boolean): string {
  const value = valueOf(preferences);
  const lifetime = value === '' ? EXPIRED : ONE_YEAR_IN_SECONDS;
  const attributes = ['path=/', `max-age=${lifetime}`, 'samesite=lax'];
  return [`${PREFERENCES_COOKIE}=${value}`, ...attributes, ...(secure ? ['secure'] : [])].join(
    '; ',
  );
}
