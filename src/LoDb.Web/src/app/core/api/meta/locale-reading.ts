import type { CatalogMeta } from '../generated/models/catalog-meta';
import type { UiLocale } from '../generated/models/ui-locale';

const REGION_SEPARATOR = '_';

function primaryOf(language: string): string {
  return language.split(REGION_SEPARATOR, 1)[0];
}

/**
 * The locale whose pages read a Data Dragon language, as `/api/meta` maps them: the locale
 * that owns it, or for a regional variant the first one sharing its language (`en_GB` is
 * read under `en`, `zh_MY` under `zh-hans`). Null for a language no locale shares.
 */
export function localeReading(meta: CatalogMeta, language: string): UiLocale | null {
  const owner = meta.locales.find((entry) => entry.language === language);
  const sibling = meta.locales.find((entry) => primaryOf(entry.language) === primaryOf(language));
  return (owner ?? sibling)?.locale ?? null;
}
