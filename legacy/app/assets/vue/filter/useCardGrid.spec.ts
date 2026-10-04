import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import { afterEach, describe, expect, it } from 'vitest'
import type { FacetDefinition } from './facets'
import { useCardGrid, type CardGrid } from './useCardGrid'

/**
 * The contract with the server-rendered markup: `data-f-<key>` attributes
 * parsed by the schema's kind, the universe of what the grid carries, and a
 * grid left untouched on teardown.
 */
const facet = (partial: Partial<FacetDefinition> & Pick<FacetDefinition, 'key' | 'kind'>): FacetDefinition => ({
    label: partial.key, group: 'g', options: [], primary: false, multiple: true, matchAll: false,
    unit: null, step: 1, ...partial,
})
const SCHEMA = [
    facet({ key: 'tag', kind: 'choice' }),
    facet({ key: 'price', kind: 'range' }),
    facet({ key: 'purchasable', kind: 'toggle' }),
]

function renderGrid(): void {
    document.body.innerHTML = `<div id="grid">
        <article data-search="Boots" data-f-tag="Boots|Armor" data-f-price="300" data-f-purchasable="1"></article>
        <article data-search="Féérique" data-f-tag="Mana" data-f-price="250" data-f-unknown="x"></article>
        <article data-search="Ward" data-f-price="oops"></article>
        <p>not a card</p>
    </div>`
}

function mountGrid(): { grid: CardGrid; unmount: () => void } {
    let grid!: CardGrid
    const wrapper = mount(defineComponent({
        setup() {
            grid = useCardGrid('grid', SCHEMA)
            grid.scan()
            return () => h('div')
        },
    }))
    return { grid, unmount: () => wrapper.unmount() }
}

afterEach(() => {
    document.body.innerHTML = ''
})

describe('useCardGrid', () => {
    it('reads the cards with their facet values, parsed by kind', () => {
        renderGrid()
        const { grid } = mountGrid()

        expect(grid.cards.value).toHaveLength(3)
        expect(grid.cards.value[0].values).toEqual({ tag: ['Boots', 'Armor'], price: 300, purchasable: true })
        expect(grid.cards.value[1].search).toBe('feerique')
        expect(grid.cards.value[1].values).toEqual({ tag: ['Mana'], price: 250 })
        expect(grid.cards.value[2].values).toEqual({})
    })

    it('collects the universe the facets may offer', () => {
        renderGrid()
        const { grid } = mountGrid()

        expect([...grid.universe.value.present.tag]).toEqual(['Boots', 'Armor', 'Mana'])
        expect(grid.universe.value.bounds.price).toEqual({ min: 250, max: 300 })
        expect(grid.universe.value.flagged.purchasable).toBe(1)
    })

    it('paints a page and restores the grid on teardown', () => {
        renderGrid()
        const { grid, unmount } = mountGrid()
        const [first, second] = grid.cards.value

        grid.show([second])
        grid.markReady()
        expect(first.el.style.display).toBe('none')
        expect(second.el.style.display).toBe('')
        expect(document.getElementById('grid')!.dataset.ready).toBe('true')

        unmount()
        expect(first.el.style.display).toBe('')
        expect(document.getElementById('grid')!.dataset.ready).toBeUndefined()
    })
})
