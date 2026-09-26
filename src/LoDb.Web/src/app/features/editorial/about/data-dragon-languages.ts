// Data Dragon's `languages.json` of the latest patches, in its order, as Data Dragon writes
// each code.
const DATA_DRAGON_CODES = [
  'ar_AE',
  'en_US',
  'cs_CZ',
  'de_DE',
  'el_GR',
  'en_AU',
  'en_GB',
  'en_PH',
  'en_SG',
  'es_AR',
  'es_ES',
  'es_MX',
  'fr_FR',
  'hu_HU',
  'id_ID',
  'it_IT',
  'ja_JP',
  'ko_KR',
  'pl_PL',
  'pt_BR',
  'ro_RO',
  'ru_RU',
  'th_TH',
  'tr_TR',
  'vi_VN',
  'zh_CN',
  'zh_MY',
  'zh_TW',
] as const;

/**
 * The languages the data is published in, as BCP 47 tags (`fr_FR` becomes `fr-FR`), for the
 * Dataset node of the About data page. Fixed here: the page is prerendered, without the API,
 * and the current site lists the same languages in the same order.
 */
export const DATA_DRAGON_LANGUAGES: readonly string[] = DATA_DRAGON_CODES.map((code) =>
  code.replace('_', '-'),
);
