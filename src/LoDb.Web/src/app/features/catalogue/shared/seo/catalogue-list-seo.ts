import type { SeoPage } from '../../../../core/seo/seo-page';
import { itemList } from '../../../../core/seo/json-ld/site/item-list';
import type { CatalogueListPage } from './catalogue-list-page';

/**
 * The SEO page of a catalogue list, for Seo.apply: canonical on the bare list whatever the
 * filters (they live in the query), self-canonical on a pinned version, and an ItemList of
 * the first entries the server rendered, linked to their pages.
 */
export function catalogueListSeo(page: CatalogueListPage): SeoPage {
  const { title, description, path, context, entries } = page;
  return {
    title,
    description,
    locale: context.locale,
    path,
    version: context.pinned ? context.version : null,
    jsonLd: (urls) => [
      itemList(entries.map((entry) => ({ name: entry.name, url: urls.page(entry.canonicalPath) }))),
    ],
  };
}
