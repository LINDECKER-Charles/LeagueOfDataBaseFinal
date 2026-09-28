import { inject } from '@angular/core';
import { Router, type UrlTree } from '@angular/router';
import type { PageContext } from '../../../../../core/context/page-context';
import { catalogueHref } from '../../links/catalogue-href';

/**
 * Builds the link of a catalogue page, `path` being its `canonicalPath` or a list's, and
 * `fragment` the id of a block of the page, such as a rune's card on its path page.
 */
export type CatalogueLink = (context: PageContext, path: string, fragment?: string) => UrlTree;

/**
 * A catalogue link for `routerLink`, as a UrlTree: a string would reach the router as a path
 * and have the `?lang=` that catalogueHref carries, or a `#fragment`, escaped into it.
 */
export function injectCatalogueLink(): CatalogueLink {
  const router = inject(Router);
  return (context, path, fragment) => {
    const tree = router.parseUrl(catalogueHref(context, path, router.url));
    tree.fragment = fragment ?? null;
    return tree;
  };
}
