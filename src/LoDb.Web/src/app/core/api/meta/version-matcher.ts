import type { CatalogMeta } from '../generated/models/catalog-meta';

/**
 * Whole-string matcher of a game version (such as `16.19.1`), built from the pattern of
 * `/api/meta`, which the document gives without anchors.
 */
export function versionMatcher(meta: CatalogMeta): RegExp {
  return new RegExp(`^(?:${meta.versionPattern})$`);
}
