import type { Locale } from '../../i18n/locales';

// The territory each locale's Data Dragon language is published for: Open Graph wants a
// language_TERRITORY pair, where the URL only names the language.
const OG_LOCALES: Record<Locale, string> = {
  ar: 'ar_AE',
  cs: 'cs_CZ',
  de: 'de_DE',
  el: 'el_GR',
  en: 'en_US',
  es: 'es_ES',
  fr: 'fr_FR',
  hu: 'hu_HU',
  id: 'id_ID',
  it: 'it_IT',
  ja: 'ja_JP',
  ko: 'ko_KR',
  pl: 'pl_PL',
  pt: 'pt_BR',
  ro: 'ro_RO',
  ru: 'ru_RU',
  th: 'th_TH',
  tr: 'tr_TR',
  vi: 'vi_VN',
  'zh-hans': 'zh_CN',
  'zh-hant': 'zh_TW',
};

/** The `og:locale` of a page. */
export function ogLocaleOf(locale: Locale): string {
  return OG_LOCALES[locale];
}
