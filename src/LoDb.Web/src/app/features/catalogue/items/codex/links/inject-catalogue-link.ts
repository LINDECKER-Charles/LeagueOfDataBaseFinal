import { inject } from '@angular/core';
import { Router, type UrlTree } from '@angular/router';
import type { PageContext } from '../../../../../core/context/page-context';
import { catalogueHref } from '../../../shared/links/catalogue-href';

/** Builds the link of a catalogue page, `path` being its `canonicalPath` or a list's. */
export type CatalogueLink = (context: PageContext, path: string) => UrlTree;

/**
 * A catalogue link for `routerLink`, as a UrlTree: a string would reach the router as a path
 * and have the `?lang=` that catalogueHref carries on escaped into it (`%3Flang%3D`).
 */
export function injectCatalogueLink(): CatalogueLink {
  const router = inject(Router);
  return (context, path) => router.parseUrl(catalogueHref(context, path, router.url));
}
