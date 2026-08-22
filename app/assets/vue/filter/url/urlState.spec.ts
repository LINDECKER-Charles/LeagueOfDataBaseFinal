import { describe, expect, it } from 'vitest'
import type { FacetDefinition } from '../facets'
import { PAGE_SIZE_ALL } from '../visibleCards'
import { parseFilterUrl, writeFilterUrl, type FilterUrlState } from './urlState'

const facet = (partial: Partial<FacetDefinition> & Pick<FacetDefinition, 'key' | 'kind'>): FacetDefinition => ({
    label: partial.key, group: 'g', options: [], primary: false, multiple: true, matchAll: false,
    unit: null, step: 1, ...partial,
})

const SCHEMA = [
    facet({ key: 'tag', kind: 'choice', matchAll: true }),
    facet({ key: 'edition', kind: 'choice', multiple: false }),
    facet({ key: 'price', kind: 'range' }),
    facet({ key: 'as', kind: 'range', step: 0.01 }),
    facet({ key: 'purchasable', kind: 'toggle' }),
]
const DEFAULT_SIZE = 12

const STATE: FilterUrlState = {
    query: 'boots',
    facets: {
        tag: { values: ['Boots', 'Armor'], all: true },
        edition: { values: ['classic'], all: false },
        price: { min: 0, max: 3000 },
        purchasable: true,
    },
    page: 2,
    size: 24,
}

describe('writeFilterUrl', () => {
    it('writes a canonical query: schema order, sorted values, explicit bounds', () => {
        expect(writeFilterUrl('', STATE, SCHEMA, DEFAULT_SIZE)).toBe(
            '?q=boots&tag=Armor%2CBoots&tag_all=1&edition=classic&price=0-3000&purchasable=1&page=2&size=24',
        )
    })

    it('omits the defaults and the empty query', () => {
        const state: FilterUrlState = { query: '  ', facets: {}, page: 1, size: DEFAULT_SIZE }
        expect(writeFilterUrl('', state, SCHEMA, DEFAULT_SIZE)).toBe('')
    })

    it('carries the foreign parameters through, ahead of its own', () => {
        const state: FilterUrlState = { query: '', facets: { purchasable: true }, page: 1, size: PAGE_SIZE_ALL }
        expect(writeFilterUrl('?lang=fr_FR&tag=Old&version=16.1.1', state, SCHEMA, DEFAULT_SIZE))
            .toBe('?lang=fr_FR&version=16.1.1&purchasable=1&size=all')
    })

    it('writes the same URL whatever the order the values were chosen in', () => {
        const a = writeFilterUrl('', { ...STATE, facets: { tag: { values: ['Boots', 'Armor'], all: false } } }, SCHEMA, DEFAULT_SIZE)
        const b = writeFilterUrl('', { ...STATE, facets: { tag: { values: ['Armor', 'Boots'], all: false } } }, SCHEMA, DEFAULT_SIZE)
        expect(a).toBe(b)
    })

    it('keeps three decimals at most on range bounds', () => {
        const state: FilterUrlState = { query: '', facets: { as: { min: 0.625, max: 0.8 } }, page: 1, size: DEFAULT_SIZE }
        expect(writeFilterUrl('', state, SCHEMA, DEFAULT_SIZE)).toBe('?as=0.625-0.8')
    })
})

describe('parseFilterUrl', () => {
    it('round-trips the written state', () => {
        const url = writeFilterUrl('', STATE, SCHEMA, DEFAULT_SIZE)
        expect(parseFilterUrl(url, SCHEMA, DEFAULT_SIZE)).toEqual({
            ...STATE,
            facets: { ...STATE.facets, tag: { values: ['Armor', 'Boots'], all: true } },
        })
    })

    it('falls back to the defaults on absent or malformed parameters', () => {
        expect(parseFilterUrl('?page=zero&size=-3&price=abc&purchasable=yes', SCHEMA, DEFAULT_SIZE))
            .toEqual({ query: '', facets: {}, page: 1, size: DEFAULT_SIZE })
    })

    it('drops an inverted range and keeps one value of a single choice', () => {
        const parsed = parseFilterUrl('?price=900-100&edition=classic,modern', SCHEMA, DEFAULT_SIZE)
        expect(parsed.facets).toEqual({ edition: { values: ['classic'], all: false } })
    })

    it('ignores the match-all flag on a facet that does not offer it', () => {
        const parsed = parseFilterUrl('?edition=classic&edition_all=1', SCHEMA, DEFAULT_SIZE)
        expect(parsed.facets.edition).toEqual({ values: ['classic'], all: false })
    })

    it('reads the ALL page size', () => {
        expect(parseFilterUrl('?size=all', SCHEMA, DEFAULT_SIZE).size).toBe(PAGE_SIZE_ALL)
    })
})
