import type { Locale } from '../../i18n/locales';

// BCP 47 writes the script subtag in title case; the URLs keep it lower case (ADR 0005).
const SCRIPT_SUBTAGS: Partial<Record<Locale, string>> = {
  'zh-hans': 'zh-Hans',
  'zh-hant': 'zh-Hant',
};

/** The BCP 47 tag of a locale, as `hreflang` and `inLanguage` expect it. */
export function hreflangOf(locale: Locale): string {
  return SCRIPT_SUBTAGS[locale] ?? locale;
}
