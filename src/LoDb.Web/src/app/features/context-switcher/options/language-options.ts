import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import type { Locale } from '../../../core/i18n/locales';
import { languageLabel, languageTag } from './language-label';
import type { LanguageOption } from './language-option';
import { primaryLanguage } from './primary-language';

function optionOf(locale: Locale, language: string, lang: string | null): LanguageOption {
  const tag = languageTag(language);
  return {
    key: `${locale}:${language}`,
    locale,
    language,
    lang,
    tag,
    label: languageLabel(language),
  };
}

/**
 * The languages the switcher offers, locale by locale in the order of `/api/meta`: each
 * locale's own Data Dragon language, then its regional variants (`en_GB` under `en`). A
 * variant is listed once, under the first locale sharing its language; a language that no
 * locale shares is left out, since no URL could carry it.
 */
export function languageOptions(meta: CatalogMeta): LanguageOption[] {
  const owns = new Set(meta.locales.map((entry) => entry.language));
  const listed = new Set<string>();
  return meta.locales.flatMap(({ locale, language }) => {
    const variants = meta.languages.filter(
      (candidate) =>
        !owns.has(candidate) &&
        !listed.has(candidate) &&
        primaryLanguage(candidate) === primaryLanguage(language),
    );
    variants.forEach((variant) => listed.add(variant));
    const own = optionOf(locale, language, null);
    return [own, ...variants.map((variant) => optionOf(locale, variant, variant))];
  });
}
