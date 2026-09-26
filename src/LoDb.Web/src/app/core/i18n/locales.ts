/**
 * The 21 site locales, in their URL form (ADR 0005). Provisional: L2.2 replaces this list
 * with the array generated from the API contract, so it is never maintained twice.
 */
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

export type Locale = (typeof LOCALES)[number];
