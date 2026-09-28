import type { FilterableCard } from './filterable-card';
import type { GridCriteria } from './grid-criteria';
import type { GridSelection } from './grid-selection';
import { matchingCards } from './matching-cards';
import { PAGE_SIZE_ALL } from './page-size-all';

/**
 * The visibility rule of a filtered list: which cards match, how many pages that makes, and
 * which cards the current page shows.
 */
export function selectVisibleCards<C extends FilterableCard>(
  cards: readonly C[],
  criteria: GridCriteria,
): GridSelection<C> {
  const matching = matchingCards(cards, criteria);
  const size =
    criteria.pageSize === PAGE_SIZE_ALL ? Math.max(1, matching.length) : criteria.pageSize;
  const pageCount = Math.max(1, Math.ceil(matching.length / size));
  // Narrowing the filter can strand the reader past the last page: back to the first one,
  // rather than to a silently different set of cards.
  const page = criteria.page > pageCount ? 1 : criteria.page;
  const start = (page - 1) * size;
  return { matching, pageCount, page, visible: matching.slice(start, start + size) };
}
