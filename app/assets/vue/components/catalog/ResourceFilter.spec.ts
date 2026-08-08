import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { afterEach, describe, expect, it } from 'vitest'
import ResourceFilter from './ResourceFilter.vue'

/**
 * Contract of the island with its server-rendered grid: it reads
 * `#grid > [data-search]`, paints exactly one page of the matching cards, and
 * restores the grid untouched on teardown (a Turbo visit reuses the same DOM).
 */
const LABELS = {
    results: '%count% results',
    empty: 'Nothing',
    clear: 'Clear',
    prev: 'Previous',
    next: 'Next',
    perPage: 'Per page',
    all: 'All',
    filters: 'Filters',
    close: 'Close',
}

const CHAMPIONS: [string, string][] = [
    ['aatrox', 'Fighter'],
    ['ahri', 'Mage'],
    ['akali', 'Assassin|Fighter'],
    ['alistar', 'Tank'],
    ['amumu', 'Tank|Mage'],
]

function renderGrid(): void {
    document.body.innerHTML = `<div id="grid">${CHAMPIONS
        .map(([name, tags]) => `<article data-search="${name}" data-tags="${tags}"></article>`)
        .join('')}</div>`
}

async function mountFilter(pageSize: number) {
    const wrapper = mount(ResourceFilter, {
        props: { gridId: 'grid', pageSize, pageSizes: [2, 4], labels: LABELS },
        attachTo: document.body,
    })
    // The grid is scanned in onMounted: let the resulting render flush.
    await nextTick()

    return wrapper
}

function visibleNames(): string[] {
    return Array.from(document.querySelectorAll<HTMLElement>('#grid > [data-search]'))
        .filter((el) => el.style.display !== 'none')
        .map((el) => el.dataset.search ?? '')
}

afterEach(() => {
    document.body.innerHTML = ''
})

describe('ResourceFilter', () => {
    it('paints the first page and flags the grid as JS-driven on mount', async () => {
        renderGrid()
        await mountFilter(2)

        expect(visibleNames()).toEqual(['aatrox', 'ahri'])
        expect(document.getElementById('grid')?.dataset.ready).toBe('true')
    })

    it('builds the facet universe from the cards, sorted and deduplicated', async () => {
        renderGrid()
        const wrapper = await mountFilter(2)

        expect(wrapper.findAll('.filter-chip').slice(0, 4).map((chip) => chip.text()))
            .toEqual(['Assassin', 'Fighter', 'Mage', 'Tank'])
    })

    it('repaints on search and reports the match count', async () => {
        renderGrid()
        const wrapper = await mountFilter(2)

        await wrapper.get('input[type="search"]').setValue('am')

        expect(visibleNames()).toEqual(['amumu'])
        expect(wrapper.text()).toContain('1 results')
    })

    it('sends a stranded page back to the first one when the filter narrows', async () => {
        renderGrid()
        const wrapper = await mountFilter(2)

        await wrapper.get('[aria-label="Next"]').trigger('click')
        expect(visibleNames()).toEqual(['akali', 'alistar'])

        await wrapper.get('input[type="search"]').setValue('a')
        await wrapper.get('input[type="search"]').setValue('ah')

        expect(visibleNames()).toEqual(['ahri'])
        expect(wrapper.text()).toContain('1 results')
    })

    it('restores every card and drops the ready flag on teardown', async () => {
        renderGrid()
        const wrapper = await mountFilter(2)
        expect(visibleNames()).toHaveLength(2)

        wrapper.unmount()

        expect(visibleNames()).toHaveLength(CHAMPIONS.length)
        expect(document.getElementById('grid')?.dataset.ready).toBeUndefined()
    })
})
