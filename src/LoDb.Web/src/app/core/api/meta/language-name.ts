// Data Dragon language → the English name the legacy site gave it (VersionManager). A code
// the table lacks falls back to `Intl`'s English name.
const LEGACY_NAMES: Readonly<Record<string, string>> = {
  ar_AE: 'Arabic (United Arab Emirates)',
  en_US: 'English (United States)',
  cs_CZ: 'Czech',
  de_DE: 'German',
  el_GR: 'Greek',
  en_AU: 'English (Australia)',
  en_GB: 'English (United Kingdom)',
  en_PH: 'English (Philippines)',
  en_SG: 'English (Singapore)',
  es_AR: 'Spanish (Argentina)',
  es_ES: 'Spanish (Spain)',
  es_MX: 'Spanish (Mexico)',
  fr_FR: 'French',
  hu_HU: 'Hungarian',
  id_ID: 'Indonesian',
  it_IT: 'Italian',
  ja_JP: 'Japanese',
  ko_KR: 'Korean',
  pl_PL: 'Polish',
  pt_BR: 'Portuguese (Brazil)',
  ro_RO: 'Romanian',
  ru_RU: 'Russian',
  th_TH: 'Thai',
  tr_TR: 'Turkish',
  vi_VN: 'Vietnamese',
  zh_CN: 'Chinese (Simplified)',
  zh_MY: 'Chinese (Malaysia)',
  zh_TW: 'Chinese (Traditional)',
};
const REGION_SEPARATOR = '_';
const TAG_SEPARATOR = '-';
const NAMES_LOCALE = 'en';

/**
 * The English name of a Data Dragon language (`fr_FR` → `French`), as every language list of
 * the legacy site showed it, whatever the page's own locale. The code itself when neither the
 * table nor `Intl` can name it.
 */
export function languageName(language: string): string {
  const legacy = LEGACY_NAMES[language];
  if (legacy !== undefined) {
    return legacy;
  }
  try {
    const names = new Intl.DisplayNames([NAMES_LOCALE], { type: 'language' });
    return names.of(language.replace(REGION_SEPARATOR, TAG_SEPARATOR)) ?? language;
  } catch {
    // An ill-formed code: the list still offers the language, under its code.
    return language;
  }
}
