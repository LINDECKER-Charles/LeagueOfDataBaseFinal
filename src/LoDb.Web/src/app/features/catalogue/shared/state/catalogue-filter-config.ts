import type { FacetDefinition } from '../facets/model/facet-definition';
import type { CatalogueListSource } from '../source/catalogue-list-source';
import type { CatalogueCardAdapter } from './catalogue-card-adapter';
import type { CatalogueListLike } from './catalogue-list-like';

/** What a list hands its filter, each read when needed, so inputs may back them. */
export interface CatalogueFilterConfig<C> {
  readonly source: () => CatalogueListSource<CatalogueListLike<C>>;
  readonly adapter: () => CatalogueCardAdapter<C>;
  readonly schema: () => readonly FacetDefinition[];
}
