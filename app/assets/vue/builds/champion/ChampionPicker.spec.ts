import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import ChampionPicker from './ChampionPicker.vue'

/**
 * Search contract of the champion grid: the player types what they see, accents
 * and case included, and the champion shows up — same comparison rule as the
 * favorites picker (a mismatch here used to make "seraphine" return nothing).
 */
const OPTIONS = [
    { id: 'Seraphine', key: '147', name: 'Séraphine', image: null },
    { id: 'Kaisa', key: '145', name: "Kai'Sa", image: null },
    { id: 'Aatrox', key: '266', name: 'Aatrox', image: null },
]

const LABELS = {
    title: 'Champion',
    search: 'Search…',
    empty: 'None.',
    selected: 'Chosen',
    open: 'Choose',
    close: 'Close',
}

const UI = {
    loading: 'Loading…',
    error: 'Unavailable.',
    retry: 'Retry',
    ghost: 'Unavailable on this patch',
    ghostMode: 'Not in this mode',
    counter: '%count% / %max%',
}

function mountPicker() {
    return mount(ChampionPicker, {
        props: {
            options: OPTIONS,
            isLoading: false,
            hasError: false,
            selectedId: '',
            labels: LABELS,
            ui: UI,
        },
    })
}

async function search(term: string): Promise<string[]> {
    const wrapper = mountPicker()
    await wrapper.get('input[type="search"]').setValue(term)
    return wrapper.findAll('.forge-champ__name').map((node) => node.text())
}

describe('ChampionPicker search', () => {
    it('ignores accents on both sides of the comparison', async () => {
        expect(await search('seraphine')).toEqual(['Séraphine'])
        expect(await search('Séraphine')).toEqual(['Séraphine'])
    })

    it('matches the id as well as the display name', async () => {
        expect(await search('kaisa')).toEqual(["Kai'Sa"])
        expect(await search("kai'sa")).toEqual(["Kai'Sa"])
    })

    it('shows the whole catalog for a blank query', async () => {
        expect(await search('   ')).toHaveLength(OPTIONS.length)
    })
})
