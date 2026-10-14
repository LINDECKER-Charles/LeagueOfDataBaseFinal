import type { SeoPage } from '../../../../core/seo/seo-page';
import { itemList } from '../../../../core/seo/json-ld/site/item-list';
import type { CatalogueListPage } from './catalogue-list-page';

const FIRST_PAGE = 1;

// A page past the first lists other entries: it is its own canonical, as Google asks of a
// paginated list, where the filters, which only narrow the same entries, are not.
function pagedPathOf(path: string, page: number | null | undefined): string {
  return page !== null && page !== undefined && page > FIRST_PAGE ? `${path}?page=${page}` : path;
}

/**
 * The SEO page of a catalogue list, for Seo.apply: canonical on the bare list whatever the
 * filters (they live in the query), on the page itself past the first, on the latest bare
 * list from a pinned version, and an ItemList of the entries the server rendered, linked to
 * their pages.
 */
export function catalogueListSeo(page: CatalogueListPage): SeoPage {
  const { title, description, path, context, entries } = page;
  return {
    title,
    description,
    locale: context.locale,
    path: pagedPathOf(path, page.page),
    version: context.pinned ? context.version : null,
    // Every list exists at the latest version, its pages listing other entries than a past one.
    canonical: { version: null, path },
    jsonLd: (urls) => [
      itemList(entries.map((entry) => ({ name: entry.name, url: urls.page(entry.canonicalPath) }))),
    ],
  };
}
