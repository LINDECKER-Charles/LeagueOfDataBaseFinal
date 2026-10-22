import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import type { CacheClass } from '../response/cache-class';

/**
 * Cache class of a catalogue page by its version: a list, the home and the page of a patch's
 * changes, whose content an older version fixes; a detail refines it by its entity's state
 * (`entryCacheClassOf`). Older than the latest, the page never changes again and is kept a
 * year. The latest, and a version listed but newer than it (soon the latest, its versioned
 * URL then a redirect), are kept thirty minutes.
 */
export function cacheClassOf(version: string, meta: CatalogMeta): CacheClass {
  const { latest, versions } = meta;
  const archived =
    !!latest && versions.includes(latest) && versions.indexOf(version) > versions.indexOf(latest);
  return archived ? 'archived' : 'latest';
}
