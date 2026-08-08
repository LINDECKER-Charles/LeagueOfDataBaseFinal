import { describe, expect, it } from 'vitest'
import { PAGE_SIZE_ALL, selectVisibleCards, type FilterableCard } from './visibleCards'

function card(search: string, ...tags: string[]): FilterableCard {
    return { search, tags }
}

const CARDS = [
    card('aatrox', 'Fighter'),
    card('ahri', 'Mage'),
    card('akali', 'Assassin', 'Fighter'),
    card('alistar', 'Tank'),
    card('amumu', 'Tank', 'Mage'),
]

function select(criteria: Partial<Parameters<typeof selectVisibleCards>[1]> = {}) {
    return selectVisibleCards(CARDS, {
        query: '', tags: new Set<string>(), page: 1, pageSize: 2, ...criteria,
    })
}

describe('selectVisibleCards — matching', () => {
    it('matches on the search haystack, case and padding insensitive', () => {
        expect(select({ query: '  AHR ' }).matching.map((c) => c.search)).toEqual(['ahri'])
    })

    it('ORs the facet tags and ANDs them with the query', () => {
        expect(select({ tags: new Set(['Tank', 'Mage']), pageSize: PAGE_SIZE_ALL }).matching)
            .toHaveLength(3)
        expect(
            select({ query: 'am', tags: new Set(['Tank']), pageSize: PAGE_SIZE_ALL }).matching
                .map((c) => c.search),
        ).toEqual(['amumu'])
    })

    it('keeps everything when neither a query nor a tag is set', () => {
        expect(select({ pageSize: PAGE_SIZE_ALL }).matching).toHaveLength(CARDS.length)
    })
})

describe('selectVisibleCards — pagination', () => {
    it('slices the requested page', () => {
        expect(select({ page: 1 }).visible.map((c) => c.search)).toEqual(['aatrox', 'ahri'])
        expect(select({ page: 3 }).visible.map((c) => c.search)).toEqual(['amumu'])
        expect(select({ page: 2 }).pageCount).toBe(3)
    })

    it('collapses to a single page for the ALL sentinel', () => {
        const all = select({ pageSize: PAGE_SIZE_ALL })
        expect(all.pageCount).toBe(1)
        expect(all.visible).toHaveLength(CARDS.length)
    })

    it('never reports fewer than one page, even with no result', () => {
        const none = select({ query: 'zzz' })
        expect(none.matching).toHaveLength(0)
        expect(none.pageCount).toBe(1)
        expect(none.visible).toEqual([])
    })

    it('sends a page stranded past the last one back to the first', () => {
        const stranded = select({ query: 'a', page: 9 })
        expect(stranded.page).toBe(1)
        expect(stranded.visible.map((c) => c.search)).toEqual(['aatrox', 'ahri'])
    })

    it('leaves an in-range page untouched', () => {
        expect(select({ page: 2 }).page).toBe(2)
    })
})
