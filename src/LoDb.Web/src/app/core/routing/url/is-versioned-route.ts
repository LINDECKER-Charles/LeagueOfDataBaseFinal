import { isCatalogueRoute } from './is-catalogue-route';
import { PATCH_CHANGES_PATH } from '../patch-changes-path';

/**
 * Whether a page (the segments below the locale and the version) carries its version in its
 * path: a list or a detail of the catalogue, or the page of a patch's changes. The context
 * switcher, the remembered version and the chrome's links follow it; the parameters a page
 * owns and the warm-up keep to the catalogue's resources (`isCatalogueRoute`).
 */
export function isVersionedRoute(page: readonly string[]): boolean {
  return isCatalogueRoute(page) || (page.length === 1 && page[0] === PATCH_CHANGES_PATH);
}
