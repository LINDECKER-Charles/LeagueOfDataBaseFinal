import type { Locale } from '../../../../../core/i18n/locales';
import type { JsonLdNode } from '../../../../../core/seo/json-ld/core/json-ld-node';
import { breadcrumbList } from '../../../../../core/seo/json-ld/site/breadcrumb-list';
import type { SeoUrls } from '../../../../../core/seo/urls/seo-urls';

/** Where a detail page sits: under the home, then its list. */
export interface DetailTrail {
  readonly locale: Locale;
  readonly homeName: string;
  readonly listName: string;
  /** The list's path, such as `items`. */
  readonly listPath: string;
  /** The entry's name, as the page's title names it. */
  readonly name: string;
}

/** The breadcrumb of a detail page: the home (never versioned), its list, the page. */
export function detailTrail(trail: DetailTrail, urls: SeoUrls): JsonLdNode {
  return breadcrumbList([
    { name: trail.homeName, url: `${urls.origin}/${trail.locale}/` },
    { name: trail.listName, url: urls.page(trail.listPath) },
    { name: trail.name, url: urls.canonical },
  ]);
}
