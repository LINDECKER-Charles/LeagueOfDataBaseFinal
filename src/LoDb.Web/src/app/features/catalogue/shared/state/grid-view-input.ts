import type { FacetDefinition } from '../facets/model/facet-definition';
import type { FilterableCard } from '../filtering/grid/filterable-card';
import type { PageSlice } from '../source/page-slice';
import type { FilterUrlState } from '../url/filter-url-state';
import type { CatalogueListLike } from './catalogue-list-like';

/** What the results column is drawn from. */
export interface GridViewInput<C> {
  /** Every card of the list, once the whole list is known; null before. */
  readonly cards: readonly FilterableCard<C>[] | null;
  /** The page the server rendered, and which one it is. */
  readonly first: CatalogueListLike<C> | null;
  readonly slice: PageSlice;
  readonly state: FilterUrlState;
  readonly schema: readonly FacetDefinition[];
}
