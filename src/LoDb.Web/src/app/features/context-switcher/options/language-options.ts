import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import { languageName } from '../../../core/api/meta/language-name';
import { localeReading } from '../../../core/api/meta/locale-reading';
import type { LanguageOption } from './language-option';

// Under the locale that reads it; `?lang=` only carries it where it is not the locale's own.
function optionFor(meta: CatalogMeta, language: string): LanguageOption | null {
  const locale = localeReading(meta, language);
  if (locale === null) {
    return null;
  }
  const own = meta.locales.some((entry) => entry.language === language);
  const lang = own ? null : language;
  return { key: `${locale}:${language}`, locale, language, lang, label: languageName(language) };
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
