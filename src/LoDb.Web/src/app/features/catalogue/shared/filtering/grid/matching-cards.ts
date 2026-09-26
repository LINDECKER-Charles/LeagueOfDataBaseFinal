import { matchesFacets } from '../../facets/rules/matches-facets';
import { normalizeSearchText } from '../../search/normalize-search-text';
import type { FilterCriteria } from './filter-criteria';
import type { FilterableCard } from './filterable-card';

/** The cards the criteria keep: the search AND every engaged facet, each its own axis. */
export function matchingCards<C extends FilterableCard>(
  cards: readonly C[],
  criteria: FilterCriteria,
): C[] {
  const needle = normalizeSearchText(criteria.query.trim());
  return cards.filter(
    (card) =>
      (needle === '' || card.search.includes(needle)) &&
      matchesFacets(card.values, criteria.facets, criteria.schema),
  );
}
