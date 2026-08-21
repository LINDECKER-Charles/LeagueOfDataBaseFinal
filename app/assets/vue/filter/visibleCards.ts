import { normalizeSearchText } from '../search/normalizeSearchText'

/**
 * Pure visibility rule of the filtered resource grid: which cards match the
 * search + facet, how many pages that makes, and which cards the current page
 * shows. No Vue, no DOM — the island only binds the result.
 */

/** Sentinel page size: a single page holding every matching card. */
export const PAGE_SIZE_ALL = 0

export interface FilterableCard {
    /** Comparison haystack of the card (already accent-folded + lowercased). */
    search: string
    tags: string[]
    /** Which game the entry belongs to ('modern' | 'classic'); absent = no such axis. */
    edition?: string
}

export interface GridCriteria {
    query: string
    tags: ReadonlySet<string>
    /** One edition to keep, or null/undefined for all of them. */
    edition?: string | null
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

export function selectVisibleCards<T extends FilterableCard>(
    cards: readonly T[],
    criteria: GridCriteria,
): GridSelection<T> {
    // Accent-folded like the pickers: "feerique" must find "féérique".
    const needle = normalizeSearchText(criteria.query.trim())
    const edition = criteria.edition ?? null
    // Query AND edition AND (any selected tag): the edition is its own axis,
    // never one more tag — "Classic + Boots" must narrow, not widen.
    const matching = cards.filter(
        (card) =>
            (needle === '' || card.search.includes(needle))
            && (edition === null || card.edition === edition)
            && (criteria.tags.size === 0 || card.tags.some((tag) => criteria.tags.has(tag))),
    )
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
