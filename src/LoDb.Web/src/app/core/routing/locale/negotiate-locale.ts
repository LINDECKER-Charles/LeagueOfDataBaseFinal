import { DEFAULT_LOCALE } from '../../i18n/default-locale';
import { isLocale } from '../../i18n/is-locale';
import type { Locale } from '../../i18n/locales';

const SUBTAG_SEPARATOR = '-';
const CHINESE = 'zh';
// Chinese is split by script. A tag that names none goes by its region: traditional
// characters in Taiwan, Hong Kong and Macao, simplified everywhere else.
const TRADITIONAL_SCRIPT = 'hant';
const SIMPLIFIED_SCRIPT = 'hans';
const TRADITIONAL_REGIONS = ['tw', 'hk', 'mo'];

function chineseLocale(subtags: readonly string[]): Locale {
  if (subtags.includes(TRADITIONAL_SCRIPT)) {
    return 'zh-hant';
  }
  if (subtags.includes(SIMPLIFIED_SCRIPT)) {
    return 'zh-hans';
  }
  return subtags.some((subtag) => TRADITIONAL_REGIONS.includes(subtag)) ? 'zh-hant' : 'zh-hans';
}

function localeOfTag(tag: string): Locale | null {
  const [primary, ...subtags] = tag.toLowerCase().split(SUBTAG_SEPARATOR);
  if (primary === CHINESE) {
    return chineseLocale(subtags);
  }
  return isLocale(primary) ? primary : null;
}

/**
 * The site locale for language tags in order of preference (`Accept-Language`, or the
 * browser's own list): the first tag whose language the site speaks wins, its region
 * aside (`pt-BR` reads `pt`, `en-GB` reads `en`). No match gives the default locale, the
 * `x-default` of ADR 0005.
 */
export function negotiateLocale(tags: readonly string[]): Locale {
  for (const tag of tags) {
    const locale = localeOfTag(tag);
    if (locale !== null) {
      return locale;
    }
  }
  return DEFAULT_LOCALE;
}
