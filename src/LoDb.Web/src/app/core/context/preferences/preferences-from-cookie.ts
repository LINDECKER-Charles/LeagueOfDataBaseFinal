import { PREFERENCES_COOKIE } from './preferences-cookie';
import { preferencesFromValue } from './preferences-from-value';
import type { Preferences } from './preferences';

const COOKIE_SEPARATOR = ';';

/** Preferences named by a `document.cookie` string, or null when there are none. */
export function preferencesFromCookie(cookies: string): Preferences | null {
  const prefix = `${PREFERENCES_COOKIE}=`;
  const entry = cookies
    .split(COOKIE_SEPARATOR)
    .map((candidate) => candidate.trim())
    .find((candidate) => candidate.startsWith(prefix));
  return entry === undefined ? null : preferencesFromValue(entry.slice(prefix.length));
}
