import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import { languageOf } from '../../api/meta/language-of';
import { versionMatcher } from '../../api/meta/version-matcher';
import type { Locale } from '../../i18n/locales';
import { parsePublicUrl } from '../../routing/url/parse-public-url';
import { pageContextOf } from '../page-context-of';
import type { Preferences } from '../preferences/preferences';
import type { NavSelection } from './nav-selection';

/** The page the chrome sits on: its URL, and the locale it speaks outside the locales. */
interface ChromePage {
  readonly url: string;
  readonly locale: Locale;
}

/**
 * The selection of a page by the rules of its own context (ADR 0005: path > query >
 * remembered, each value checked against `/api/meta`), so a link never pins a version the
 * catalogue does not list, nor a variant of another language. Null while no version is
 * ingested. Unlike the page's resolvers, it reads the query of every page (`/about?version=`)
 * and what the browser remembers, as the legacy navigation did.
 */
export function navSelectionOf(
  page: ChromePage,
  meta: CatalogMeta,
  remembered: Preferences | null,
): NavSelection | null {
  const matcher = versionMatcher(meta);
  const url = parsePublicUrl(page.url, (segment) => matcher.test(segment));
  const locale = url.locale ?? page.locale;
  const sources = { locale, path: url.version, query: url.query.toString(), remembered };
  const context = pageContextOf(sources, meta);
  if (context === null) {
    return null;
  }
  const variant = context.language === languageOf(meta, locale) ? null : context.language;
  return {
    shown: context.version,
    version: context.pinned ? context.version : null,
    lang: variant,
  };
}
