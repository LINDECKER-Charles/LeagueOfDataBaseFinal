import type { CatalogMeta } from '../generated/models/catalog-meta';
import type { UiLocale } from '../generated/models/ui-locale';

/**
 * The Data Dragon language a locale's pages read by default, as `/api/meta` maps it. A
 * locale the document does not map reads the language every version ships.
 */
export function languageOf(meta: CatalogMeta, locale: UiLocale): string {
  return meta.locales.find((entry) => entry.locale === locale)?.language ?? meta.defaultLanguage;
}
