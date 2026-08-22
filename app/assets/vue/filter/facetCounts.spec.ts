import { describe, expect, it } from 'vitest'
import { countFacetOptions } from './facetCounts'
import type { FacetDefinition } from './facets'

/**
 * The faceted-search convention: a value is counted under every OTHER
 * engaged axis, its own selection lifted — except in match-all mode, where
 * the selection only narrows and therefore stays.
 */
const facet = (partial: Partial<FacetDefinition> & Pick<FacetDefinition, 'key' | 'kind'>): FacetDefinition => ({
    label: partial.key, group: 'g', options: [], primary: false, multiple: true, matchAll: false,
    unit: null, step: 1, ...partial,
})

const SCHEMA = [
    facet({ key: 'tag', kind: 'choice', matchAll: true }),
    facet({ key: 'edition', kind: 'choice', multiple: false }),
    facet({ key: 'price', kind: 'range' }),
    facet({ key: 'purchasable', kind: 'toggle' }),
]

const card = (search: string, tag: string[], edition: string, price: number, purchasable = false) => ({
    search,
    values: { tag, edition: [edition], price, ...(purchasable ? { purchasable: true as const } : {}) },
})
const CARDS = [
    card('boots', ['Boots', 'Armor'], 'modern', 300, true),
    card('boots classic', ['Boots'], 'classic', 300, true),
    card('dagger', ['Damage'], 'modern', 250),
    card('ward', ['Vision'], 'modern', 0, true),
]

const count = (query: string, facets: Parameters<typeof countFacetOptions>[1]['facets']) =>
    countFacetOptions(CARDS, { query, facets, schema: SCHEMA })

describe('countFacetOptions', () => {
    it('tallies every value of every card when nothing is engaged', () => {
        const counts = count('', {})
        expect([...counts.options.tag]).toEqual([['Boots', 2], ['Armor', 1], ['Damage', 1], ['Vision', 1]])
        expect([...counts.options.edition]).toEqual([['modern', 3], ['classic', 1]])
        expect(counts.flagged.purchasable).toBe(3)
        expect(counts.options.price).toBeUndefined()
    })

    it('counts a facet under the other axes but not under its own selection', () => {
        const counts = count('', { edition: { values: ['classic'], all: false } })
        // Tags narrowed to the classic card…
        expect([...counts.options.tag]).toEqual([['Boots', 1]])
        // …while editions keep counting everything: "what you would get".
        expect([...counts.options.edition]).toEqual([['modern', 3], ['classic', 1]])
        expect(counts.flagged.purchasable).toBe(1)
    })

    it('keeps the selection of a match-all choice, which can only narrow', () => {
        const counts = count('', { tag: { values: ['Boots'], all: true } })
        expect([...counts.options.tag]).toEqual([['Boots', 2], ['Armor', 1]])
        expect([...counts.options.edition]).toEqual([['modern', 1], ['classic', 1]])
    })

    it('applies the search and the ranges like every other axis', () => {
        expect([...count('boots', {}).options.edition]).toEqual([['modern', 1], ['classic', 1]])
        const counts = count('', { price: { min: 250, max: 300 } })
        expect([...counts.options.tag]).toEqual([['Boots', 2], ['Armor', 1], ['Damage', 1]])
        expect(counts.flagged.purchasable).toBe(2)
    })
})
