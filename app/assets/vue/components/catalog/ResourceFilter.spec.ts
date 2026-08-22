import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { FacetDefinition } from '../../filter/facets'
import ResourceFilter from './ResourceFilter.vue'

/**
 * Contract of the island with its server-rendered grid and the page URL: it
 * reads `#grid > [data-search]` and the `data-f-*` values, paints exactly one
 * page of the matching cards, offers only the facets the grid carries, starts
 * from the URL and writes the state back to it — and restores the grid
 * untouched on teardown (a Turbo visit reuses the same DOM).
 */
const LABELS = {
    results: '%count% results', empty: 'Nothing', clear: 'Clear', prev: 'Previous', next: 'Next',
    perPage: 'Per page', all: 'All', filters: 'Filters', close: 'Close', advanced: 'Advanced',
    matchAny: 'Any', matchAll: 'All of', min: 'Min', max: 'Max', copy: 'Copy', copied: 'Copied',
    copyError: 'Copy below', active: 'Active',
}

const facet = (partial: Partial<FacetDefinition> & Pick<FacetDefinition, 'key' | 'kind'>): FacetDefinition => ({
    label: partial.key, group: 'Category', options: [], primary: false, multiple: true, matchAll: false,
    unit: null, step: 1, ...partial,
})
const FACETS = [
    facet({ key: 'tag', kind: 'choice', primary: true, matchAll: true, options: [
        { value: 'Boots', label: 'Boots' }, { value: 'Damage', label: 'Damage' }, { value: 'Vision', label: 'Vision' },
    ] }),
    facet({ key: 'edition', kind: 'choice', primary: true, multiple: false, options: [
        { value: 'modern', label: 'Current' }, { value: 'classic', label: 'LoL Classic' },
    ] }),
    facet({ key: 'price', kind: 'range', group: 'Price' }),
    facet({ key: 'consumable', kind: 'toggle', group: 'Availability' }),
]

/** Items carry an edition: the LoL Classic twin of Boots shares its name. */
const ITEMS: [string, string, string, number][] = [
    ['boots 1001', 'Boots', 'modern', 300],
    ['dagger 1042', 'Damage', 'modern', 250],
    ['boots 771001', 'Boots', 'classic', 300],
]

function renderGrid(): void {
    document.body.innerHTML = `<div id="grid">${ITEMS
        .map(([name, tag, edition, price]) =>
            `<article data-search="${name}" data-f-tag="${tag}" data-f-edition="${edition}" data-f-price="${price}"></article>`)
        .join('')}</div>`
}

async function mountFilter(pageSize = 2, facets = FACETS) {
    const wrapper = mount(ResourceFilter, {
        props: { gridId: 'grid', pageSize, pageSizes: [2, 4], facets, labels: LABELS },
        attachTo: document.body,
    })
    // The grid is scanned in onMounted: let the resulting render flush.
    await nextTick()
    return wrapper
}

const visible = () =>
    Array.from(document.querySelectorAll<HTMLElement>('#grid > article'))
        .filter((el) => el.style.display !== 'none')
        .map((el) => el.dataset.search)

const chip = (wrapper: ReturnType<typeof mount>, text: string) =>
    wrapper.findAll('.filter-chip').find((b) => b.text() === text)!

beforeEach(() => {
    vi.useFakeTimers()
    window.history.replaceState(null, '', '/objects')
    renderGrid()
})

afterEach(() => {
    vi.useRealTimers()
    document.body.innerHTML = ''
})

describe('ResourceFilter', () => {
    it('paints the first page and hands paint control to JS', async () => {
        await mountFilter()

        expect(visible()).toEqual(['boots 1001', 'dagger 1042'])
        expect(document.getElementById('grid')!.dataset.ready).toBe('true')
    })

    it('offers only the facets the grid carries', async () => {
        const wrapper = await mountFilter()

        const chips = wrapper.findAll('.facet--inline .filter-chip').map((b) => b.text())
        expect(chips).toEqual(['Boots', 'Damage', 'Current', 'LoL Classic'])
        // A flag no card carries is not offered; the price range is (250 < 300).
        expect(wrapper.find('.facet--toggle').exists()).toBe(false)
        expect(wrapper.find('.filter-trigger[aria-expanded]').exists()).toBe(true)
    })

    it('narrows by search and facets, ANDing the axes, and counts', async () => {
        const wrapper = await mountFilter(4)

        await chip(wrapper, 'Boots').trigger('click')
        expect(visible()).toEqual(['boots 1001', 'boots 771001'])
        await chip(wrapper, 'LoL Classic').trigger('click')
        expect(visible()).toEqual(['boots 771001'])
        expect(wrapper.text()).toContain('1 results')
        expect(wrapper.find('.filter-trigger b').text()).toBe('2')
    })

    it('starts from the URL and writes the state back to it', async () => {
        window.history.replaceState(null, '', '/objects?lang=fr_FR&tag=Damage&size=4')
        const wrapper = await mountFilter()

        expect(visible()).toEqual(['dagger 1042'])
        await wrapper.find('input[type="search"]').setValue('dag')
        await chip(wrapper, 'Boots').trigger('click')
        vi.advanceTimersByTime(400)

        expect(window.location.search).toBe('?lang=fr_FR&q=dag&tag=Boots%2CDamage&size=4')
        // Chips read in the order the values were chosen; the URL is the sorted one.
        expect(wrapper.find('.active-filters').text()).toContain('tag: Damage, Boots')
    })

    it('sends a stranded page back to the first one when the filter narrows', async () => {
        const wrapper = await mountFilter()
        await wrapper.findAll('.filter-nav')[1].trigger('click')
        expect(visible()).toEqual(['boots 771001'])

        await wrapper.find('input[type="search"]').setValue('boots')
        expect(visible()).toEqual(['boots 1001', 'boots 771001'])
    })

    it('clears everything at once', async () => {
        const wrapper = await mountFilter(4)
        await chip(wrapper, 'Boots').trigger('click')
        await wrapper.find('input[type="search"]').setValue('771')
        expect(visible()).toEqual(['boots 771001'])

        await wrapper.find('.active-filters .filter-clear').trigger('click')

        expect(visible()).toHaveLength(3)
        expect(wrapper.find('.active-filters').exists()).toBe(false)
    })

    it('restores the grid on teardown', async () => {
        const wrapper = await mountFilter()
        wrapper.unmount()

        expect(visible()).toHaveLength(3)
        expect(document.getElementById('grid')!.dataset.ready).toBeUndefined()
    })
})
