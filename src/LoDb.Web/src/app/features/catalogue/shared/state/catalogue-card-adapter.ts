import type { CardValues } from '../facets/model/card-values';

/**
 * How the filter reads the cards of one list, given by its page: what the search matches
 * (raw text, normalized by the filter), the values each facet key reads, and a stable key.
 */
export interface CatalogueCardAdapter<C> {
  // Methods, not function properties: an adapter of one card type then stands for the
  // filter's unknown cards.
  searchTextOf(card: C): string;
  valuesOf(card: C): CardValues;
  keyOf(card: C): string;
}
