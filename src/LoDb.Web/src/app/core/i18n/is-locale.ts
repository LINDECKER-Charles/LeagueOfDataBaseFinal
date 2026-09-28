import { LOCALES, type Locale } from './locales';

/** Case-sensitive on purpose: `/EN/` is not a canonical URL and must not render as one. */
export function isLocale(value: unknown): value is Locale {
  return LOCALES.some((locale) => locale === value);
}
