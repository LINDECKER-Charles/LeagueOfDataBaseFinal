import { DEFAULT_LOCALE } from '../../../../core/i18n/default-locale';
import { isLocale } from '../../../../core/i18n/is-locale';
import type { Locale } from '../../../../core/i18n/locales';

const REGION_SEPARATOR = '_';
const TRADITIONAL_CHINESE = 'zh_TW';
const CHINESE = 'zh';

/**
 * The interface locale that suits a Data Dragon language (`heritage.md` § 6): its base code
 * (`pt_BR` → `pt`), Taiwan's Chinese in traditional characters and every other Chinese in
 * simplified ones, `en` for a language no locale speaks. LoDb.Domain's `UiLocales` has the
 * twin.
 */
export function localeOfLanguage(language: string | null | undefined): Locale {
  if (!language) {
    return DEFAULT_LOCALE;
  }
  const base = language.split(REGION_SEPARATOR, 1)[0].toLowerCase();
  if (base === CHINESE) {
    return language.startsWith(TRADITIONAL_CHINESE) ? 'zh-hant' : 'zh-hans';
  }
  return isLocale(base) ? base : DEFAULT_LOCALE;
}
