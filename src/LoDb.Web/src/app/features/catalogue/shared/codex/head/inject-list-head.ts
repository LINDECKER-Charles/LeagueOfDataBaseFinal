import { effect, inject, untracked } from '@angular/core';
import type { SeoPage } from '../../../../../core/seo/seo-page';
import { catalogueListSeo } from '../../seo/catalogue-list-seo';
import { CatalogueHead } from './catalogue-head';
import type { CatalogueTexts } from './catalogue-texts';
import type { ListHead } from './list-head';

function seoOf(head: ListHead, texts: CatalogueTexts): SeoPage {
  const { context, count } = head;
  const description =
    count === null
      ? texts.main('base.description')
      : texts.seo(`${head.texts}.list.description`, { count, version: context.version });
  const page = catalogueListSeo({
    title: texts.seo(`${head.texts}.list.title`),
    description,
    path: head.path,
    context,
    entries: head.entries,
  });
  return { ...page, image: `/preview/${head.path}.png` };
}

/**
 * Writes the head of a list page each time what it says changes: the texts of its locale,
 * the count and an ItemList of the first page the server rendered. `head` returns null
 * while there is nothing to say yet, such as a list still on its way.
 */
export function injectListHead(head: () => ListHead | null): void {
  const writer = inject(CatalogueHead);
  effect(() => {
    const current = head();
    if (current !== null) {
      untracked(() => writer.write(current.context.locale, (texts) => seoOf(current, texts)));
    }
  });
}
