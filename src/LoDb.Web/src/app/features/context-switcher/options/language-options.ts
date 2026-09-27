import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import { languageName } from '../../../core/api/meta/language-name';
import type { Locale } from '../../../core/i18n/locales';
import type { LanguageOption } from './language-option';
import { primaryLanguage } from './primary-language';

function optionOf(locale: Locale, language: string, lang: string | null): LanguageOption {
  return { key: `${locale}:${language}`, locale, language, lang, label: languageName(language) };
}

// The locale whose pages read `language`: its owner, or for a regional variant the first
// locale sharing its language (`en_GB` under `en`, `zh_MY` under `zh-hans`); none otherwise.
function optionFor(meta: CatalogMeta, language: string): LanguageOption | null {
  const owner = meta.locales.find((entry) => entry.language === language);
  if (owner !== undefined) {
    return optionOf(owner.locale, language, null);
  }
  const sibling = meta.locales.find(
    (entry) => primaryLanguage(entry.language) === primaryLanguage(language),
  );
  return sibling === undefined ? null : optionOf(sibling.locale, language, language);
}

/**
 * The languages the switcher offers, in Data Dragon's own order (`/api/meta`), as the legacy
 * switcher listed them. Each is read under the locale it belongs to; a language that no
 * locale shares is left out, since no URL could carry it.
 */
export function languageOptions(meta: CatalogMeta): LanguageOption[] {
  return meta.languages
    .map((language) => optionFor(meta, language))
    .filter((option) => option !== null);
}
