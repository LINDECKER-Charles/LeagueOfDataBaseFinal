import { describe, expect, it } from 'vitest'
import { normalizeSearchText } from '../search/normalizeSearchText'
import type { FacetDefinition, FacetState } from './facets'
import { PAGE_SIZE_ALL, selectVisibleCards, type FilterableCard, type GridCriteria } from './visibleCards'

const TAG: FacetDefinition = {
    key: 'tag', kind: 'choice', label: 'Tag', group: 'g', options: [], primary: true,
    multiple: true, matchAll: false, unit: null, step: 1,
}
const SCHEMA = [TAG]

const card = (search: string, tags: string[] = []): FilterableCard => ({ search, values: { tag: tags } })

const CARDS = [
    card('aatrox', ['Fighter']),
    card('ahri', ['Mage']),
    card('akali', ['Assassin', 'Fighter']),
    card('alistar', ['Tank']),
    card('amumu', ['Tank', 'Mage']),
]

function select(criteria: Partial<GridCriteria> = {}) {
    return selectVisibleCards(CARDS, {
        query: '', facets: {}, schema: SCHEMA, page: 1, pageSize: PAGE_SIZE_ALL, ...criteria,
    })
}

const tags = (...values: string[]): FacetState => ({ tag: { values, all: false } })

describe('selectVisibleCards — matching', () => {
    it('matches the query case-insensitively and ignores padding', () => {
        expect(select({ query: '  AKA ' }).matching.map((c) => c.search)).toEqual(['akali'])
    })

    it('folds accents so "feerique" finds "féérique"', () => {
        // Haystacks arrive folded from useCardGrid; the needle is folded here.
        const result = selectVisibleCards([card(normalizeSearchText('charme féérique'))], {
            query: 'feerique', facets: {}, schema: SCHEMA, page: 1, pageSize: PAGE_SIZE_ALL,
        })
        expect(result.matching).toHaveLength(1)
    })

    it('ORs the chosen values and ANDs them with the query', () => {
        expect(select({ facets: tags('Tank', 'Mage') }).matching.map((c) => c.search))
            .toEqual(['ahri', 'alistar', 'amumu'])
        expect(select({ query: 'am', facets: tags('Tank') }).matching.map((c) => c.search))
            .toEqual(['amumu'])
    })

    it('keeps every card when nothing is engaged', () => {
        expect(select().matching).toHaveLength(5)
    })
})

describe('selectVisibleCards — pagination', () => {
    it('slices the requested page', () => {
        const result = select({ page: 2, pageSize: 2 })
        expect(result.pageCount).toBe(3)
        expect(result.visible.map((c) => c.search)).toEqual(['akali', 'alistar'])
    })

    it('shows everything on a single page under the ALL sentinel', () => {
        const result = select({ pageSize: PAGE_SIZE_ALL })
        expect(result.pageCount).toBe(1)
        expect(result.visible).toHaveLength(5)
    })

    it('never reports fewer than one page', () => {
        expect(select({ query: 'zzz', pageSize: 2 }).pageCount).toBe(1)
    })

    it('sends a stranded page back to the first one', () => {
        const result = select({ page: 3, pageSize: 2, facets: tags('Tank') })
        expect(result.page).toBe(1)
        expect(result.visible.map((c) => c.search)).toEqual(['alistar', 'amumu'])
    })

    it('leaves an in-range page untouched', () => {
        expect(select({ page: 2, pageSize: 2 }).page).toBe(2)
    })
})
