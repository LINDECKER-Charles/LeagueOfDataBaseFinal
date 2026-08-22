import { describe, expect, it } from 'vitest'
import {
    activeFacetCount,
    countEngaged,
    groupFacets,
    isGroupOpenByDefault,
    matchesFacets,
    withChoiceMatchAll,
    withChoiceToggled,
    withRange,
    withToggle,
    type FacetDefinition,
} from './facets'

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

const BOOTS = { tag: ['Boots', 'Armor'], edition: ['modern'], price: 300, purchasable: true as const }
const WARD = { tag: ['Vision'], edition: ['modern'], price: 0 }

describe('matchesFacets', () => {
    it('passes every card when nothing is engaged', () => {
        expect(matchesFacets(WARD, {}, SCHEMA)).toBe(true)
    })

    it('ORs the values of a choice, ANDs them in match-all mode', () => {
        expect(matchesFacets(BOOTS, { tag: { values: ['Boots', 'Vision'], all: false } }, SCHEMA)).toBe(true)
        expect(matchesFacets(BOOTS, { tag: { values: ['Boots', 'Vision'], all: true } }, SCHEMA)).toBe(false)
        expect(matchesFacets(BOOTS, { tag: { values: ['Boots', 'Armor'], all: true } }, SCHEMA)).toBe(true)
    })

    it('bounds a range inclusively and rejects cards without the value', () => {
        expect(matchesFacets(BOOTS, { price: { min: 300, max: 300 } }, SCHEMA)).toBe(true)
        expect(matchesFacets(BOOTS, { price: { min: 301, max: 900 } }, SCHEMA)).toBe(false)
        expect(matchesFacets({ tag: ['Boots'] }, { price: { min: 0, max: 900 } }, SCHEMA)).toBe(false)
    })

    it('keeps only flagged cards under a toggle', () => {
        expect(matchesFacets(BOOTS, { purchasable: true }, SCHEMA)).toBe(true)
        expect(matchesFacets(WARD, { purchasable: true }, SCHEMA)).toBe(false)
    })

    it('ANDs across facets', () => {
        const state = { tag: { values: ['Boots'], all: false }, price: { min: 0, max: 100 } }
        expect(matchesFacets(BOOTS, state, SCHEMA)).toBe(false)
    })
})

describe('transitions', () => {
    it('toggles a value in and out, dropping the facet when empty', () => {
        const on = withChoiceToggled({}, SCHEMA[0], 'Boots')
        expect(on).toEqual({ tag: { values: ['Boots'], all: false } })
        expect(withChoiceToggled(on, SCHEMA[0], 'Boots')).toEqual({})
    })

    it('replaces the value of a single-choice facet instead of accumulating', () => {
        const state = withChoiceToggled({ edition: { values: ['modern'], all: false } }, SCHEMA[1], 'classic')
        expect(state.edition).toEqual({ values: ['classic'], all: false })
    })

    it('switches match mode only on an engaged choice', () => {
        expect(withChoiceMatchAll({}, 'tag', true)).toEqual({})
        const state = withChoiceMatchAll({ tag: { values: ['a'], all: false } }, 'tag', true)
        expect(state.tag).toEqual({ values: ['a'], all: true })
    })

    it('sets and clears ranges and toggles', () => {
        const ranged = withRange({}, 'price', { min: 0, max: 10 })
        expect(ranged).toEqual({ price: { min: 0, max: 10 } })
        expect(withRange(ranged, 'price', null)).toEqual({})
        expect(withToggle({}, 'purchasable', true)).toEqual({ purchasable: true })
        expect(withToggle({ purchasable: true }, 'purchasable', false)).toEqual({})
    })

    it('counts engaged facets, whatever the number of chosen values', () => {
        expect(activeFacetCount({
            tag: { values: ['a', 'b'], all: false },
            price: { min: 0, max: 1 },
            purchasable: true,
        })).toBe(3)
    })
})

describe('groupFacets', () => {
    const grouped = [
        facet({ key: 'role', kind: 'choice', group: 'Profile', primary: true }),
        facet({ key: 'hp', kind: 'range', group: 'Stats' }),
        facet({ key: 'range', kind: 'choice', group: 'Profile' }),
        facet({ key: 'armor', kind: 'range', group: 'Stats' }),
    ]

    it('keeps the schema order of groups and of facets within them', () => {
        expect(groupFacets(grouped).map((g) => [g.name, g.facets.map((f) => f.key)])).toEqual([
            ['Profile', ['role', 'range']],
            ['Stats', ['hp', 'armor']],
        ])
    })

    it('counts the engaged facets of a group', () => {
        const [profile, stats] = groupFacets(grouped)
        const state = { role: { values: ['Tank'], all: false }, hp: { min: 500, max: 600 } }
        expect(countEngaged(profile.facets, state)).toBe(1)
        expect(countEngaged(stats.facets, state)).toBe(1)
        expect(countEngaged(stats.facets, {})).toBe(0)
    })

    it('unfolds a group by default for a primary facet or an engaged one', () => {
        const [profile, stats] = groupFacets(grouped)
        expect(isGroupOpenByDefault(profile, {})).toBe(true)
        expect(isGroupOpenByDefault(stats, {})).toBe(false)
        expect(isGroupOpenByDefault(stats, { armor: { min: 20, max: 40 } })).toBe(true)
    })
})
