import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import { versionMatcher } from '../../../core/api/meta/version-matcher';
import { pageContextOf } from '../../../core/context/page-context-of';
import type { Locale } from '../../../core/i18n/locales';
import { parsePublicUrl } from '../../../core/routing/url/parse-public-url';
import type { LanguageOption } from '../options/language-option';
import type { SwitcherChoice } from './switcher-choice';

/** The page the switcher sits on: its URL and the locale it speaks. */
interface ShownPage {
  readonly url: string;
  readonly locale: Locale;
}

/**
 * What the switcher shows as selected on a page: the context it reads, by the rules of the
 * routing (path > query, the latest version and the locale's own language by default). The
 * cookie is not a source here: once applied, the URL names it. A page outside the locales
 * (`/b/…`) reads the locale it speaks. Null while no version is ingested.
 */
export function currentChoice(
  shown: ShownPage,
  meta: CatalogMeta,
  options: readonly LanguageOption[],
): SwitcherChoice | null {
  const matcher = versionMatcher(meta);
  const page = parsePublicUrl(shown.url, (segment) => matcher.test(segment));
  const sources = {
    locale: page.locale ?? shown.locale,
    path: page.version,
    query: page.query.toString(),
    remembered: null,
  };
  const context = pageContextOf(sources, meta);
  if (context === null) {
    return null;
  }
  const ofLocale = options.filter((option) => option.locale === context.locale);
  // A variant listed under a sibling locale (`zh_MY` under `zh-hans` on `/zh-hant/`) is not
  // offered here: picking it would change the locale. The locale's own entry stands for it.
  const selected =
    ofLocale.find((option) => option.language === context.language) ??
    ofLocale.find((option) => option.lang === null);
  return { version: context.version, language: selected?.key ?? '' };
}
