import type { FilterableCard } from '../filtering/grid/filterable-card';
import { normalizeSearchText } from '../search/normalize-search-text';
import type { CatalogueCardAdapter } from './catalogue-card-adapter';

/** The cards of a list as the filter reads them, their haystack normalized once. */
export function filterableCardsOf<C>(
  cards: readonly C[],
  adapter: CatalogueCardAdapter<C>,
): FilterableCard<C>[] {
  return cards.map((card) => ({
    card,
    search: normalizeSearchText(adapter.searchTextOf(card)),
    values: adapter.valuesOf(card),
  }));
}
