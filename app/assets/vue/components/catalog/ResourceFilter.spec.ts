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

/** Items carry an edition: the LoL Classic twin of Boots shares its name. */
const ITEMS: [string, string, string][] = [
    ['boots 1001', 'Boots', 'modern'],
    ['dagger 1042', 'Damage', 'modern'],
    ['boots 771001', 'Boots', 'classic'],
]

const EDITIONS = { modern: 'Current', classic: 'LoL Classic' }
const EDITION_ALL = 'Every edition'

function renderGrid(): void {
    document.body.innerHTML = `<div id="grid">${CHAMPIONS
        .map(([name, tags]) => `<article data-search="${name}" data-tags="${tags}"></article>`)
        .join('')}</div>`
}

function renderItemGrid(): void {
    document.body.innerHTML = `<div id="grid">${ITEMS
        .map(([name, tags, edition]) =>
            `<article data-search="${name}" data-tags="${tags}"
                      data-edition="${edition}"></article>`)
        .join('')}</div>`
}

async function mountFilter(pageSize: number) {
    const wrapper = mount(ResourceFilter, {
        props: {
            gridId: 'grid', pageSize, pageSizes: [2, 4],
            labels: { ...LABELS, edition: 'Edition', editionAll: EDITION_ALL },
            editions: EDITIONS,
        },
        attachTo: document.body,
    })
    // The grid is scanned in onMounted: let the resulting render flush.
    await nextTick()

    return wrapper
}

function editionChips(wrapper: ReturnType<typeof mount>) {
    return wrapper.findAll('[role="group"] .filter-chip--edition')
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

    it('offers the edition switch only when the grid mixes editions', async () => {
        renderGrid()
        const single = await mountFilter(4)
        expect(editionChips(single)).toHaveLength(0)
        single.unmount()

        renderItemGrid()
        const wrapper = await mountFilter(4)

        // Desktop row + mobile sheet each list "All" + the editions present.
        expect(editionChips(wrapper)).toHaveLength(6)
        expect(editionChips(wrapper).slice(0, 3).map((chip) => chip.text()))
            .toEqual([EDITION_ALL, 'Current', 'LoL Classic'])
    })

    it('narrows to one edition, ANDed with the tag facet, and clears with the rest', async () => {
        renderItemGrid()
        const wrapper = await mountFilter(4)

        await editionChips(wrapper)[2].trigger('click')
        expect(visibleNames()).toEqual(['boots 771001'])
        expect(editionChips(wrapper)[2].attributes('aria-pressed')).toBe('true')
        // The edition counts as one active facet on the mobile trigger.
        expect(wrapper.get('.filter-trigger b').text()).toBe('1')

        const tagChips = wrapper.findAll('.filter-chip:not(.filter-chip--edition)')
        await tagChips[1].trigger('click') // Damage
        expect(visibleNames()).toEqual([])
        expect(wrapper.text()).toContain('0 results')

        await wrapper.get('.filter-clear').trigger('click')
        expect(visibleNames()).toHaveLength(ITEMS.length)
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
