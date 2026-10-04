import type { CatalogMeta } from '../../../core/api/generated/models/catalog-meta';
import { versionMatcher } from '../../../core/api/meta/version-matcher';
import { pageContextOf } from '../../../core/context/page-context-of';
import type { Preferences } from '../../../core/context/preferences/preferences';
import type { Locale } from '../../../core/i18n/locales';
import { parsePublicUrl } from '../../../core/routing/url/parse-public-url';
import type { LanguageOption } from '../options/language-option';
import type { SwitcherChoice } from './switcher-choice';

const LANG_PARAM = 'lang';

/** The page the switcher sits on: its URL, the locale it speaks, the context kept. */
interface ShownPage {
  readonly url: string;
  readonly locale: Locale;
  /** This session's choice, else the remembered one (`PreferencesStore.current`). */
  readonly remembered: Preferences | null;
}

/**
 * What the switcher shows as selected on a page: the context it reads, by the rules of the
 * routing (path > query > the context kept, the latest version and the locale's own language
 * by default). The kept context names the patch of the pages that read none (`/about`), as
 * the legacy `page_selection` did, so the chip agrees with the chrome's links. A page outside
 * the locales (`/b/…`) reads the language its `?lang=` names, whichever locale owns it, else
 * the locale it speaks. Null while no version is ingested.
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
    remembered: shown.remembered,
  };
  const context = pageContextOf(sources, meta);
  if (context === null) {
    return null;
  }
  const outside = page.locale === null ? page.query.get(LANG_PARAM) : null;
  const named = options.find((option) => outside !== null && option.language === outside);
  if (named !== undefined) {
    return { version: context.version, language: named.key };
  }
  const ofLocale = options.filter((option) => option.locale === context.locale);
  // A variant listed under a sibling locale (`zh_MY` under `zh-hans` on `/zh-hant/`) is not
  // offered here: picking it would change the locale. The locale's own entry stands for it.
  const selected =
    ofLocale.find((option) => option.language === context.language) ??
    ofLocale.find((option) => option.lang === null);
  return { version: context.version, language: selected?.key ?? '' };
}
