import { mount } from '@vue/test-utils'
import { beforeAll, describe, expect, it, vi } from 'vitest'
import ItemArmory from './ItemArmory.vue'

/**
 * Browse-and-add contract of the armory: the catalog is filtered by the shared
 * search rule (accent/case-insensitive) intersected with the category chips.
 */
const ITEMS = [
    { id: '3153', name: 'Lame du Roi Déchu', image: null, gold: 3100, purchasable: true,
        tags: ['Damage', 'LifeSteal'] },
    { id: '3020', name: 'Chaussures de Sorcier', image: null, gold: 1100, purchasable: true,
        tags: ['Boots', 'MagicPenetration'] },
    { id: '1055', name: "Lame de Doran", image: null, gold: 450, purchasable: true,
        tags: ['Damage', 'Health'] },
]

const LABELS = {
    title: 'Armory',
    addCta: 'Add item',
    search: 'Search an item…',
    empty: 'None.',
    done: 'Done',
    close: 'Close the armory',
    added: '%count% added',
    inStep: '%count% in this step',
    full: 'Step full',
    categories: {
        all: 'All',
        attack: 'Attack',
        magic: 'Magic',
        defense: 'Defense',
        mobility: 'Mobility',
        utility: 'Utility',
    },
}

const UI = {
    loading: 'Loading…',
    error: 'Unavailable.',
    retry: 'Retry',
    ghost: 'Unavailable on this patch',
    ghostMode: 'Not in this mode',
    counter: '%count% / %max%',
}

beforeAll(() => {
    HTMLDialogElement.prototype.showModal = vi.fn()
    HTMLDialogElement.prototype.close = vi.fn()
})

function mountArmory() {
    return mount(ItemArmory, {
        props: {
            open: true,
            step: { index: 0, label: 'Start', items: [] },
            options: ITEMS,
            isLoading: false,
            hasError: false,
            canAdd: true,
            maxItems: 8,
            labels: LABELS,
            ui: UI,
        },
        attachTo: document.body,
    })
}

function names(wrapper: ReturnType<typeof mountArmory>): string[] {
    return wrapper.findAll('.armory-item__name').map((node) => node.text())
}

describe('ItemArmory search', () => {
    it('ignores accents and case', async () => {
        const wrapper = mountArmory()
        await wrapper.get('input[type="search"]').setValue('dechu')
        expect(names(wrapper)).toEqual(['Lame du Roi Déchu'])
    })

    it('intersects the query with the active category chip', async () => {
        const wrapper = mountArmory()
        await wrapper.get('input[type="search"]').setValue('lame')
        expect(names(wrapper)).toHaveLength(2)

        // "Mobility" is the boots bucket: no "lame" item belongs to it.
        const chips = wrapper.findAll('.armory__cat')
        await chips[4]?.trigger('click')
        expect(names(wrapper)).toEqual([])
    })

    it('lists the whole catalog for a blank query', () => {
        expect(names(mountArmory())).toHaveLength(ITEMS.length)
    })
})
