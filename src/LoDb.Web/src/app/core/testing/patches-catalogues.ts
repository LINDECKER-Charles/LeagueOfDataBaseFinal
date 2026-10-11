import type { Translation } from '@jsverse/transloco';
import AR from '../../../../public/i18n/patches/ar.json';
import CS from '../../../../public/i18n/patches/cs.json';
import DE from '../../../../public/i18n/patches/de.json';
import EL from '../../../../public/i18n/patches/el.json';
import EN from '../../../../public/i18n/patches/en.json';
import ES from '../../../../public/i18n/patches/es.json';
import FR from '../../../../public/i18n/patches/fr.json';
import HU from '../../../../public/i18n/patches/hu.json';
import ID from '../../../../public/i18n/patches/id.json';
import IT from '../../../../public/i18n/patches/it.json';
import JA from '../../../../public/i18n/patches/ja.json';
import KO from '../../../../public/i18n/patches/ko.json';
import PL from '../../../../public/i18n/patches/pl.json';
import PT from '../../../../public/i18n/patches/pt.json';
import RO from '../../../../public/i18n/patches/ro.json';
import RU from '../../../../public/i18n/patches/ru.json';
import TH from '../../../../public/i18n/patches/th.json';
import TR from '../../../../public/i18n/patches/tr.json';
import VI from '../../../../public/i18n/patches/vi.json';
import ZH_HANS from '../../../../public/i18n/patches/zh-hans.json';
import ZH_HANT from '../../../../public/i18n/patches/zh-hant.json';
import type { Locale } from '../i18n/locales';

/**
 * The patch history's catalogue of each locale (`public/i18n/patches/`), as the build ships
 * it, for the specs that check its texts are translated everywhere. Here rather than in the
 * feature: a feature may import nothing outside `core/`, `ui/` and itself.
 */
export const PATCHES_CATALOGUES: Readonly<Record<Locale, Translation>> = {
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
