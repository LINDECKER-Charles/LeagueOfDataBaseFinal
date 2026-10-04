import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import type { CacheClass } from '../response/cache-class';

/**
 * Cache class of a catalogue page by its version. Older than the latest, the page never
 * changes again and is kept a week. The latest, and a version listed but newer than it
 * (soon the latest, its versioned URL then a redirect), are kept five minutes.
 */
export function cacheClassOf(version: string, meta: CatalogMeta): CacheClass {
  const { latest, versions } = meta;
  const archived =
    !!latest && versions.includes(latest) && versions.indexOf(version) > versions.indexOf(latest);
  return archived ? 'archived' : 'latest';
}
