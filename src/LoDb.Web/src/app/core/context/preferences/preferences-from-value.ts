import { isLocale } from '../../i18n/is-locale';
import type { Locale } from '../../i18n/locales';
import { QueryString } from '../../routing/url/query-string';
import { PREFERENCES_FIELDS } from './preferences-fields';
import type { Preferences } from './preferences';

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

function localeField(value: QueryString): Locale | null | undefined {
  const raw = value.get(PREFERENCES_FIELDS.locale);
  if (raw === null || raw === '') {
    return null;
  }
  return isLocale(raw) ? raw : undefined;
}

/**
 * Preferences in their stored form (`preferencesValue`), or null when there are none. A value
 * that is empty, from the legacy format or tampered with gives null rather than an error: a
 * bad cookie must never break a page.
 */
export function preferencesFromValue(stored: string): Preferences | null {
  const value = QueryString.parse(stored);
  const locale = localeField(value);
  const lang = field(value, PREFERENCES_FIELDS.lang);
  const version = field(value, PREFERENCES_FIELDS.version);
  if (locale === undefined || lang === undefined || version === undefined) {
    return null;
  }
  if (locale === null && lang === null && version === null) {
    return null;
  }
  return locale === null ? { lang, version } : { lang, version, locale };
}
