import { normalizeSearchText } from '../search/normalizeSearchText'
import { matchesFacets, type CardValues, type FacetDefinition, type FacetState } from './facets'

/**
 * Pure visibility rule of the filtered resource grid: which cards match the
 * search + facets, how many pages that makes, and which cards the current
 * page shows. No Vue, no DOM — the island only binds the result.
 */

/** Sentinel page size: a single page holding every matching card. */
export const PAGE_SIZE_ALL = 0

export interface FilterableCard {
    /** Comparison haystack of the card (already accent-folded + lowercased). */
    search: string
    values: CardValues
}

/** What narrows the grid: the search text and the engaged facets. */
export interface FilterCriteria {
    query: string
    facets: FacetState
    schema: readonly FacetDefinition[]
}

export interface GridCriteria extends FilterCriteria {
    page: number
    /** {@link PAGE_SIZE_ALL} or a positive page size. */
    pageSize: number
}

export interface GridSelection<T> {
    matching: T[]
    pageCount: number
    /** The requested page, sent back to 1 when it fell past the last one. */
    page: number
    visible: T[]
}

/** The cards the criteria keep — query AND every engaged facet, each its own axis. */
export function matchingCards<T extends FilterableCard>(
    cards: readonly T[],
    criteria: FilterCriteria,
): T[] {
    // Accent-folded like the pickers: "feerique" must find "féérique".
    const needle = normalizeSearchText(criteria.query.trim())
    return cards.filter(
        (card) =>
            (needle === '' || card.search.includes(needle))
            && matchesFacets(card.values, criteria.facets, criteria.schema),
    )
}

export function selectVisibleCards<T extends FilterableCard>(
    cards: readonly T[],
    criteria: GridCriteria,
): GridSelection<T> {
    const matching = matchingCards(cards, criteria)
    const size = criteria.pageSize === PAGE_SIZE_ALL
        ? Math.max(1, matching.length)
        : criteria.pageSize
    const pageCount = Math.max(1, Math.ceil(matching.length / size))
    // Narrowing the filter can strand the reader past the last page: send them
    // back to the first one rather than to a silently different set of cards.
    const page = criteria.page > pageCount ? 1 : criteria.page
    const start = (page - 1) * size
    return { matching, pageCount, page, visible: matching.slice(start, start + size) }
}
