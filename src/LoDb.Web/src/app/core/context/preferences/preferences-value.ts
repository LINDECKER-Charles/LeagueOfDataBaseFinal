import { PREFERENCES_FIELDS } from './preferences-fields';
import type { Preferences } from './preferences';

/**
 * The stored form of `preferences` (`loc=fr&l=en_GB&v=15.14.1`), '' when there is nothing to
 * keep. A null field is left out: it means "the default", which needs no remembering.
 */
export function preferencesValue(preferences: Preferences | null): string {
  const fields = new URLSearchParams();
  if (preferences?.locale) {
    fields.set(PREFERENCES_FIELDS.locale, preferences.locale);
  }
  if (preferences?.lang) {
    fields.set(PREFERENCES_FIELDS.lang, preferences.lang);
  }
  if (preferences?.version) {
    fields.set(PREFERENCES_FIELDS.version, preferences.version);
  }
  return fields.toString();
}
