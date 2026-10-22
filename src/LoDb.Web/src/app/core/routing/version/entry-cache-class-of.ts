import type { CatalogMeta } from '../../api/generated/models/catalog-meta';
import type { EntityState } from '../../api/generated/models/entity-state';
import type { CacheClass } from '../response/cache-class';
import { cacheClassOf } from './cache-class-of';

/**
 * Cache class of a detail by the state of its entity, as the API renders it. A page of an
 * older version is frozen (`archived`) only once its state is closed: the page of the open
 * state changes when a promotion closes it (`revisable`), and so may a page whose state the
 * API does not know (outside the index, or the index lagging), never kept a year. The latest
 * version, and a version newer than it, keep the version's class.
 */
export function entryCacheClassOf(
  version: string,
  meta: CatalogMeta,
  state: EntityState | null,
): CacheClass {
  const byVersion = cacheClassOf(version, meta);
  if (byVersion !== 'archived') {
    return byVersion;
  }
  return state !== null && !state.isOpen ? 'archived' : 'revisable';
}
