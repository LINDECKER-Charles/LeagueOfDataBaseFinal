/** The 21 locales of ADR 0005, in the order of the hreflang alternates. */
export const LOCALES = [
  'ar',
  'cs',
  'de',
  'el',
  'en',
  'es',
  'fr',
  'hu',
  'id',
  'it',
  'ja',
  'ko',
  'pl',
  'pt',
  'ro',
  'ru',
  'th',
  'tr',
  'vi',
  'zh-hans',
  'zh-hant',
] as const;

/** The URL of a page in a locale, `/{locale}/{path}`. */
export function pageUrl(locale: string, path: string): string {
  return `/${locale}/${path}`;
}
