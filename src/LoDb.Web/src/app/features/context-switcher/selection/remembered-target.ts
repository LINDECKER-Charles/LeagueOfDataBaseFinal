import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import { languageOf } from '../../../core/api/meta/language-of';
import { versionMatcher } from '../../../core/api/meta/version-matcher';
import type { Preferences } from '../../../core/context/preferences/preferences';
import type { ContextTarget } from '../../../core/context/switch/context-target';
import type { Locale } from '../../../core/i18n/locales';
import { isCatalogueRoute } from '../../../core/routing/url/is-catalogue-route';
import { parsePublicUrl } from '../../../core/routing/url/parse-public-url';
import { primaryLanguage } from '../options/primary-language';

const VERSION_PARAM = 'version';
const LANG_PARAM = 'lang';

// Only the home page and the catalogue read a version and a language: elsewhere the cookie
// would only add a query the page ignores.
function readsContext(page: readonly string[]): boolean {
  return page.length === 0 || isCatalogueRoute(page);
}

// An older version `/api/meta` lists; the latest is what the URL already follows.
function olderVersion(version: string | null, meta: CatalogMeta): string | null {
  return version !== null && version !== meta.latest && meta.versions.includes(version)
    ? version
    : null;
}

// A regional variant of the locale's own language (`en_GB` on `/en/`), never another one.
function variantOf(lang: string | null, locale: Locale, meta: CatalogMeta): string | null {
  const own = languageOf(meta, locale);
  const isVariant =
    lang !== null &&
    lang !== own &&
    meta.languages.includes(lang) &&
    primaryLanguage(lang) === primaryLanguage(own);
  return isVariant ? lang : null;
}

/**
 * What the remembered context changes on `url`, or null when nothing: per axis, the cookie
 * only fills what neither the path nor the query names (ADR 0005: path > query > cookie),
 * and only with a value `/api/meta` still lists. The server never reads the cookie, so the
 * browser applies it after the page renders, by rewriting the URL with `switchContext`.
 */
export function rememberedTarget(
  url: string,
  remembered: Preferences,
  meta: CatalogMeta,
): ContextTarget | null {
  const matcher = versionMatcher(meta);
  const page = parsePublicUrl(url, (segment) => matcher.test(segment));
  if (page.locale === null || !readsContext(page.page)) {
    return null;
  }
  const namesVersion = page.version !== null || page.query.get(VERSION_PARAM) !== null;
  const version = namesVersion ? null : olderVersion(remembered.version, meta);
  const namesLang = page.query.get(LANG_PARAM) !== null;
  const lang = namesLang ? null : variantOf(remembered.lang, page.locale, meta);
  if (version === null && lang === null) {
    return null;
  }
  return { ...(version === null ? {} : { version }), ...(lang === null ? {} : { lang }) };
}
