import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import { hasChangesPage } from '../../api/meta/has-changes-page';
import { PATCH_CHANGES_PATH } from '../../routing/patch-changes-path';

/**
 * `version` when the page (the segments below the locale and the version) exists at it, else
 * null for the latest, the short URL. Every page does, but a patch's changes: only the
 * versions from `oldestChangesVersion` up to `newestChangesVersion` (the latest, unless the
 * index lags) have one, and pinning any other one (the first covered, one the index has not
 * reached) would lead the visitor to a 404.
 */
export function pinnableVersion(
  page: readonly string[],
  version: string | null,
  meta: CatalogMeta,
): string | null {
  const isChanges = page.length === 1 && page[0] === PATCH_CHANGES_PATH;
  return version === null || !isChanges || hasChangesPage(version, meta) ? version : null;
}
