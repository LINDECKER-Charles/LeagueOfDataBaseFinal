import { type Signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import type { NavSelection } from '../../context/nav/nav-selection';
import type { Locale } from '../../i18n/locales';
import { cataloguePath } from '../../routing/url/catalogue-path';
import { isCatalogueRoute } from '../../routing/url/is-catalogue-route';
import { PageDirection } from '../direction/page-direction';
import { localePath } from '../shell/locale-path';
import type { NavEntry } from '../shell/nav-entry';
import type { ChromeLink } from './chrome-link';
import { NavContext } from './nav-context';

const LATEST: Pick<NavSelection, 'version' | 'lang'> = { version: null, lang: null };

// Only the catalogue keeps the page's version and variant, as the legacy navigation did:
// the home page, the trends or the editorial pages have one address whatever the patch.
function chromePath(locale: Locale, path: string, selection: typeof LATEST): string {
  return isCatalogueRoute([path])
    ? cataloguePath(locale, path, selection)
    : localePath(locale, path);
}

/**
 * The chrome's links to `entries` from the current page, in its locale, the catalogue ones
 * pinned to the page's version and language variant (legacy `resource_path` with
 * `page_selection`), so moving around never drops a patch the visitor chose. Their active
 * state follows: `/en/14.1.1/items` lights the items entry of `/en/14.1.1/items/1036`.
 */
export function injectChromeLinks(entries: readonly NavEntry[]): Signal<readonly ChromeLink[]> {
  const router = inject(Router);
  const page = inject(PageDirection);
  const nav = inject(NavContext);
  return computed(() => {
    const locale = page.locale();
    const selection = nav.selection() ?? LATEST;
    return entries.map((entry) => ({
      entry,
      target: router.parseUrl(chromePath(locale, entry.path, selection)),
    }));
  });
}
