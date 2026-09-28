import { PAGE_SIZE_ALL } from '../filtering/grid/page-size-all';
import { isFilterEngaged } from '../filtering/grid/is-filter-engaged';
import { selectVisibleCards } from '../filtering/grid/select-visible-cards';
import type { PageSlice } from '../source/page-slice';
import type { FilterUrlState } from '../url/filter-url-state';
import type { CatalogueListLike } from './catalogue-list-like';
import type { GridView } from './grid-view';
import type { GridViewInput } from './grid-view-input';

function isSlice(state: FilterUrlState, slice: PageSlice): boolean {
  return (
    !isFilterEngaged(state.query, state.facets) &&
    state.page === slice.page &&
    state.size === slice.size
  );
}

// The page the server rendered, as it is: its cards, the list's total, the pages it makes.
function sliceView<C>(first: CatalogueListLike<C>, slice: PageSlice): GridView<C> {
  const size = slice.size === PAGE_SIZE_ALL ? Math.max(1, first.total) : slice.size;
  const pageCount = Math.max(1, Math.ceil(first.total / size));
  const { entries: visible, total } = first;
  return { visible, matching: total, total, page: slice.page, pageCount, isSkeleton: false };
}

/**
 * The results column. With the whole list, the filter decides; before it, the page the
 * server rendered is shown as long as the state asks for exactly that page, which is what
 * crawlers and the first paint read. Any other state waits for the whole list.
 */
export function gridViewOf<C>({
  cards,
  first,
  slice,
  state,
  schema,
}: GridViewInput<C>): GridView<C> {
  if (cards !== null) {
    const { query, facets, page, size: pageSize } = state;
    const selection = selectVisibleCards(cards, { query, facets, schema, page, pageSize });
    const visible = selection.visible.map((card) => card.card);
    const { page: shown, pageCount } = selection;
    const matching = selection.matching.length;
    return { visible, matching, total: cards.length, page: shown, pageCount, isSkeleton: false };
  }
  if (first !== null && isSlice(state, slice)) {
    return sliceView(first, slice);
  }
  const total = first?.total ?? 0;
  return { visible: [], matching: total, total, page: state.page, pageCount: 1, isSkeleton: true };
}
