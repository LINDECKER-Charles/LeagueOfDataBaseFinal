import type { Translation } from '@jsverse/transloco';
import AR from '../../../../public/i18n/ar.json';
import CS from '../../../../public/i18n/cs.json';
import DE from '../../../../public/i18n/de.json';
import EL from '../../../../public/i18n/el.json';
import EN from '../../../../public/i18n/en.json';
import ES from '../../../../public/i18n/es.json';
import FR from '../../../../public/i18n/fr.json';
import HU from '../../../../public/i18n/hu.json';
import ID from '../../../../public/i18n/id.json';
import IT from '../../../../public/i18n/it.json';
import JA from '../../../../public/i18n/ja.json';
import KO from '../../../../public/i18n/ko.json';
import PL from '../../../../public/i18n/pl.json';
import PT from '../../../../public/i18n/pt.json';
import RO from '../../../../public/i18n/ro.json';
import RU from '../../../../public/i18n/ru.json';
import TH from '../../../../public/i18n/th.json';
import TR from '../../../../public/i18n/tr.json';
import VI from '../../../../public/i18n/vi.json';
import ZH_HANS from '../../../../public/i18n/zh-hans.json';
import ZH_HANT from '../../../../public/i18n/zh-hant.json';
import type { Locale } from '../i18n/locales';

/**
 * The root catalogue of each locale, as the build ships it, for the specs that check a key
 * is translated everywhere rather than left to the `en` fallback.
 */
export const ROOT_CATALOGUES: Readonly<Record<Locale, Translation>> = {
  ar: AR,
  cs: CS,
  de: DE,
  el: EL,
  en: EN,
  es: ES,
  fr: FR,
  hu: HU,
  id: ID,
  it: IT,
  ja: JA,
  ko: KO,
  pl: PL,
  pt: PT,
  ro: RO,
  ru: RU,
  th: TH,
  tr: TR,
  vi: VI,
  'zh-hans': ZH_HANS,
  'zh-hant': ZH_HANT,
};
