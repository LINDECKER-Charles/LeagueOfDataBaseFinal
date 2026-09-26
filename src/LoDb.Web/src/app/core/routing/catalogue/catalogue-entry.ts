import type { PageContext } from '../../context/page-context';

/**
 * What a detail route resolves: the page's context and the entity, fetched once by the
 * routing, which needs its `canonicalPath`, and handed to the page, which must not fetch
 * it again (plan, section 5.2: never a double fetch).
 */
export interface CatalogueEntry<T> {
  readonly context: PageContext;
  readonly details: T;
}
