import { QueryString } from '../../routing/url/query-string';
import { PREFERENCES_COOKIE } from './preferences-cookie';
import { PREFERENCES_FIELDS } from './preferences-fields';
import type { Preferences } from './preferences';

const COOKIE_SEPARATOR = ';';
// Letters, digits, `_` and `.` spell every language and version: anything else was not
// written by this site.
const SAFE_VALUE = /^[\w.]{1,32}$/;

// Null for an absent or empty field, undefined for a value this site never writes.
function field(value: QueryString, name: string): string | null | undefined {
  const raw = value.get(name);
  if (raw === null || raw === '') {
    return null;
  }
  return SAFE_VALUE.test(raw) ? raw : undefined;
}

/**
 * Preferences named by a `document.cookie` string, or null when there are none. A value
 * that is absent, empty, from the legacy format or tampered with gives null rather than
 * an error: a bad cookie must never break a page.
 */
export function preferencesFromCookie(cookies: string): Preferences | null {
  const prefix = `${PREFERENCES_COOKIE}=`;
  const entry = cookies
    .split(COOKIE_SEPARATOR)
    .map((candidate) => candidate.trim())
    .find((candidate) => candidate.startsWith(prefix));
  if (entry === undefined) {
    return null;
  }
  const value = QueryString.parse(entry.slice(prefix.length));
  const lang = field(value, PREFERENCES_FIELDS.lang);
  const version = field(value, PREFERENCES_FIELDS.version);
  if (lang === undefined || version === undefined || (lang === null && version === null)) {
    return null;
  }
  return { lang, version };
}
