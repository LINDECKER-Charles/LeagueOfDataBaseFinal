import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import { languageOf } from '../../api/meta/language-of';
import { versionMatcher } from '../../api/meta/version-matcher';
import type { Locale } from '../../i18n/locales';
import { formatPublicUrl } from '../../routing/url/format-public-url';
import { isCatalogueRoute } from '../../routing/url/is-catalogue-route';
import { parsePublicUrl } from '../../routing/url/parse-public-url';
import type { PublicUrl } from '../../routing/url/public-url';
import type { QueryString } from '../../routing/url/query-string';
import type { ContextTarget } from './context-target';

const VERSION_PARAM = 'version';
const LANG_PARAM = 'lang';

// An empty value (`?lang=`) says nothing, like an absent one.
function kept(target: string | null | undefined, current: string | null): string | null {
  return (target === undefined ? current : target) || null;
}

// The locale's own language is implied by the URL: writing it would give the same page
// a second address.
function variantOf(lang: string | null, locale: Locale, meta: CatalogMeta): string | null {
  return lang === languageOf(meta, locale) ? null : lang;
}

// Outside the locales (`/b/…`) no path carries a language: `?lang=` names every one, the
// chosen locale's own included, and is kept when the target names none.
function outsideLang(target: ContextTarget, current: PublicUrl, meta: CatalogMeta): string | null {
  if (target.lang) {
    return target.lang;
  }
  return target.locale === undefined
    ? kept(target.lang, current.query.get(LANG_PARAM))
    : languageOf(meta, target.locale);
}

// The parameters the switcher does not own keep their place; `lang`, then `version` when
// the path cannot carry it, come last.
function queryOf(current: PublicUrl, lang: string | null, version: string | null): QueryString {
  const others = current.query.without(VERSION_PARAM, LANG_PARAM);
  const withLang = lang === null ? others : others.with(LANG_PARAM, lang);
  return version === null ? withLang : withLang.with(VERSION_PARAM, version);
}

/**
 * The URL of the same page in another context (ADR 0005): a pure rewrite, so switching is
 * one navigation, never a chain of redirects. The locale replaces the first segment; the
 * version goes into the path of the catalogue pages, into `?version=` elsewhere, and only
 * when it is not the latest; the language goes into `?lang=` unless it is the locale's
 * own; a page outside the locales, whose path has none, reads any language from `?lang=`.
 * The rest of the query and the fragment survive. The path wins over the query.
 */
export function switchContext(url: string, target: ContextTarget, meta: CatalogMeta): string {
  const matcher = versionMatcher(meta);
  const current = parsePublicUrl(url, (segment) => matcher.test(segment));
  const locale = current.locale === null ? null : (target.locale ?? current.locale);
  const requested = kept(target.version, current.version ?? current.query.get(VERSION_PARAM));
  const version = requested === meta.latest ? null : requested;
  const lang =
    locale === null
      ? outsideLang(target, current, meta)
      : variantOf(kept(target.lang, current.query.get(LANG_PARAM)), locale, meta);
  const inPath = locale !== null && isCatalogueRoute(current.page);
  const query = queryOf(current, lang, inPath ? null : version);
  return formatPublicUrl({ ...current, locale, version: inPath ? version : null, query });
}
