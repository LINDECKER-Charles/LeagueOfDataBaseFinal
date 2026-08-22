import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { FacetDefinition } from '../../filter/facets'
import ResourceFilter from './ResourceFilter.vue'

/**
 * Contract of the island with its server-rendered grid, the layout slots and
 * the page URL: it reads `#grid > [data-search]` and the `data-f-*` values,
 * paints exactly one page of the matching cards, offers only the facets the
 * grid carries (counted in context), fills the `-bar` / `-head` / `-empty`
 * slots around the grid, starts from the URL and writes the state back to it
 * — and restores the grid untouched on teardown (a Turbo visit reuses the
 * same DOM).
 */
const LABELS = {
    results: '%count% results', resultsOne: '%count% result', page: 'page %page% / %count%',
    gauge: 'Results', empty: 'Nothing',
    emptyTitle: 'No results', emptyCta: 'Clear filters', clear: 'Clear', clearAll: 'Clear all',
    prev: 'Previous', next: 'Next', perPage: 'Per page', all: 'All', filters: 'Filters',
    showResults: 'Show %count% results', showResultsOne: 'Show %count% result',
    matchMode: 'Combine', matchAny: 'Any', matchAll: 'All of',
    min: 'Min', max: 'Max', copy: 'Copy', copied: 'Copied', copyError: 'Copy below', active: 'Active',
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
const ITEMS: [string, string, string, number, boolean?][] = [
    ['boots 1001', 'Boots', 'modern', 300],
    ['dagger 1042', 'Damage', 'modern', 250],
    ['boots 771001', 'Boots', 'classic', 300],
]
const POTION: (typeof ITEMS)[number] = ['potion 2003', 'Consumable', 'modern', 50, true]

function renderLayout(items = ITEMS): void {
    const cards = items
        .map(([name, tag, edition, price, isConsumable]) =>
            `<article data-search="${name}" data-f-tag="${tag}" data-f-edition="${edition}" data-f-price="${price}"`
            + `${isConsumable ? ' data-f-consumable="1"' : ''}></article>`)
        .join('')
    document.body.innerHTML = `
        <div id="grid-bar"></div>
        <div id="grid-head"></div>
        <div id="grid">${cards}</div>
        <div id="grid-empty"></div>`
}

async function mountFilter(pageSize = 2, facets = FACETS) {
    const host = document.createElement('aside')
    document.body.prepend(host)
    const wrapper = mount(ResourceFilter, {
        props: { gridId: 'grid', pageSize, pageSizes: [2, 4], facets, labels: LABELS },
        attachTo: host,
    })
    // The grid is scanned in onMounted: let the resulting render flush.
    await nextTick()
    return wrapper
}

const visible = () =>
    Array.from(document.querySelectorAll<HTMLElement>('#grid > article'))
        .filter((el) => el.style.display !== 'none')
        .map((el) => el.dataset.search)

/** A chip reads "<label> <count>"; the console (wrapper root) holds the desktop set. */
const chip = (wrapper: ReturnType<typeof mount>, label: string) =>
    wrapper.findAll('.filter-console .filter-chip').find((b) => b.text().startsWith(label))!
const chipText = (wrapper: ReturnType<typeof mount>) =>
    wrapper.findAll('.filter-console .filter-chip').map((b) => b.text().replace(/\s+/g, ' '))
const slot = (id: string) => document.getElementById(id)!
const click = async (el: Element | null) => {
    el!.dispatchEvent(new MouseEvent('click', { bubbles: true }))
    await nextTick()
}

beforeEach(() => {
    vi.useFakeTimers()
    window.history.replaceState(null, '', '/objects')
    renderLayout()
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

    it('offers only the facets the grid carries, counted, primary groups unfolded', async () => {
        const wrapper = await mountFilter()

        expect(chipText(wrapper)).toEqual(['Boots 2', 'Damage 1', 'Current 2', 'LoL Classic 1'])
        // A flag no card carries is not offered: its group does not exist.
        expect(wrapper.findAll('.filter-console .facet-group__name').map((g) => g.text()))
            .toEqual(['Category', 'Price'])
        expect(wrapper.find('.facet--toggle').exists()).toBe(false)
        // The price range (250 < 300) waits folded in its non-primary group.
        expect(wrapper.find('.filter-console .facet--range').exists()).toBe(false)
        await wrapper.findAll('.filter-console .facet-group__title')[1].trigger('click')
        expect(wrapper.find('.filter-console .facet--range').exists()).toBe(true)
    })

    it('fills the bar, head and empty slots around the grid', async () => {
        await mountFilter()

        expect(slot('grid-bar').querySelector('.filter-trigger')).not.toBeNull()
        expect(slot('grid-head').textContent).toContain('3')
        expect(slot('grid-head').textContent).toContain('page 1 / 2')
        expect(slot('grid-empty').querySelector('.filter-empty')).toBeNull()
    })

    it('narrows by search and facets, ANDing the axes, and recounts in context', async () => {
        const wrapper = await mountFilter(4)

        await chip(wrapper, 'Boots').trigger('click')
        expect(visible()).toEqual(['boots 1001', 'boots 771001'])
        // Edition is counted among the Boots only; tags keep counting on their own axis.
        expect(chipText(wrapper)).toEqual(['Boots 2', 'Damage 1', 'Current 1', 'LoL Classic 1'])
        await chip(wrapper, 'LoL Classic').trigger('click')
        expect(visible()).toEqual(['boots 771001'])
        // Nothing classic carries Damage: the chip stays visible but cannot be chosen.
        expect(chipText(wrapper)).toEqual(['Boots 1', 'Damage 0', 'Current 1', 'LoL Classic 1'])
        expect(chip(wrapper, 'Damage').attributes('disabled')).toBeDefined()
        expect(slot('grid-head').querySelector('.filter-toolbar__count-n')!.textContent).toBe('1')
        expect(slot('grid-bar').querySelector('.filter-trigger b')!.textContent).toBe('2')
        expect(wrapper.find('.filter-gauge__value').text()).toBe('1 / 3')
    })

    it('starts from the URL and writes the state back to it', async () => {
        window.history.replaceState(null, '', '/objects?lang=fr_FR&tag=Damage&size=4')
        const wrapper = await mountFilter()

        expect(visible()).toEqual(['dagger 1042'])
        await chip(wrapper, 'Boots').trigger('click')
        await wrapper.find('.filter-console input[type="search"]').setValue('dag')
        vi.advanceTimersByTime(400)

        expect(window.location.search).toBe('?lang=fr_FR&q=dag&tag=Boots%2CDamage&size=4')
        // Chips read in the order the values were chosen; the URL is the sorted one.
        expect(slot('grid-head').querySelector('.active-filters')!.textContent).toContain('tag: Damage, Boots')
    })

    it('sends a stranded page back to the first one when the filter narrows', async () => {
        const wrapper = await mountFilter()
        await click(slot('grid-head').querySelectorAll('.filter-nav')[1])
        expect(visible()).toEqual(['boots 771001'])

        await wrapper.find('.filter-console input[type="search"]').setValue('boots')
        expect(visible()).toEqual(['boots 1001', 'boots 771001'])
    })

    it('shows the empty state and clears everything from it', async () => {
        const wrapper = await mountFilter(4)
        await chip(wrapper, 'Boots').trigger('click')
        await wrapper.find('.filter-console input[type="search"]').setValue('dagger')
        expect(visible()).toEqual([])
        expect(slot('grid-empty').querySelector('.filter-empty__title')!.textContent).toBe('No results')

        await click(slot('grid-empty').querySelector('.filter-empty .hx-btn'))

        expect(visible()).toHaveLength(3)
        expect(slot('grid-head').querySelector('.active-filters')).toBeNull()
        expect(slot('grid-empty').querySelector('.filter-empty')).toBeNull()
    })

    it('jumps to the search field on "/" unless the reader is already typing', async () => {
        const wrapper = await mountFilter()
        const input = wrapper.find<HTMLInputElement>('.filter-console input[type="search"]').element
        // jsdom lays nothing out: stand in for a visible field.
        Object.defineProperty(input, 'offsetParent', { value: document.body })

        const event = new KeyboardEvent('keydown', { key: '/', cancelable: true })
        window.dispatchEvent(event)
        expect(document.activeElement).toBe(input)
        expect(event.defaultPrevented).toBe(true)

        const typed = new KeyboardEvent('keydown', { key: '/', cancelable: true })
        window.dispatchEvent(typed)
        expect(typed.defaultPrevented).toBe(false)
    })

    it('keeps a group unfolded once engaged, even after its last facet is cleared', async () => {
        const wrapper = await mountFilter(4)
        await wrapper.findAll('.filter-console .facet-group__title')[1].trigger('click')
        const minField = wrapper.find<HTMLInputElement>('.filter-console .facet__field input')
        // Fields commit on change only: typing never rewrites them under the reader.
        await minField.setValue('300')
        await minField.trigger('change')
        expect(visible()).toEqual(['boots 1001', 'boots 771001'])
        expect(wrapper.find('.filter-console .facet-group__badge').text()).toBe('1')

        await wrapper.find('.filter-console .facet__reset').trigger('click')

        expect(visible()).toHaveLength(3)
        expect(wrapper.find('.filter-console .facet--range').exists()).toBe(true)
    })

    it('offers a flag as a counted switch', async () => {
        renderLayout([...ITEMS, POTION])
        const wrapper = await mountFilter(4)
        await wrapper.findAll('.filter-console .facet-group__title')[2].trigger('click')
        const toggle = wrapper.find('.filter-console .facet--toggle')
        expect(toggle.text()).toContain('1')

        await toggle.find('input').setValue(true)

        expect(visible()).toEqual(['potion 2003'])
        // No boots are consumable: the chip reads 0 and cannot be chosen.
        expect(chip(wrapper, 'Boots').text()).toBe('Boots 0')
        expect(chip(wrapper, 'Boots').attributes('disabled')).toBeDefined()
    })

    it('opens the sheet from the bar, reading the live count on its call to action', async () => {
        await mountFilter(4)
        const dialog = document.querySelector<HTMLDialogElement>('dialog.filter-sheet')!
        dialog.showModal = vi.fn(() => dialog.setAttribute('open', ''))
        dialog.close = vi.fn(() => dialog.removeAttribute('open'))

        await click(slot('grid-bar').querySelector('.filter-trigger'))
        expect(dialog.open).toBe(true)
        expect(dialog.querySelector('.filter-sheet__done')!.textContent).toBe('Show 3 results')
        expect((dialog.querySelector('.filter-sheet__clear') as HTMLButtonElement).disabled).toBe(true)

        await click(Array.from(dialog.querySelectorAll('.filter-chip')).find((b) => b.textContent!.includes('Damage'))!)
        expect(dialog.querySelector('.filter-sheet__done')!.textContent).toBe('Show 1 result')
        expect((dialog.querySelector('.filter-sheet__clear') as HTMLButtonElement).disabled).toBe(false)

        await click(dialog.querySelector('.filter-sheet__done'))
        expect(dialog.open).toBe(false)
        expect(visible()).toEqual(['dagger 1042'])
    })

    it('leaves an empty server-rendered grid to its own empty state', async () => {
        renderLayout([])
        await mountFilter()

        expect(slot('grid-empty').querySelector('.filter-empty')).toBeNull()
    })

    it('restores the grid on teardown', async () => {
        const wrapper = await mountFilter()
        wrapper.unmount()

        expect(visible()).toHaveLength(3)
        expect(document.getElementById('grid')!.dataset.ready).toBeUndefined()
        expect(slot('grid-head').children).toHaveLength(0)
    })
})
