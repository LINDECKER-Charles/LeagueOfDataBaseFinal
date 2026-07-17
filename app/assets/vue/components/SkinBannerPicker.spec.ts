import { mount } from '@vue/test-utils'
import { describe, expect, it, vi, afterEach } from 'vitest'
import SkinBannerPicker from './SkinBannerPicker.vue'

/**
 * Contract of the island with its Twig host: the single hidden input carries the
 * "{championId}_{skinNum}" banner id the profile form submits, seeded from the
 * server-resolved `current` and updated only by an explicit pick/remove.
 */
const LABELS = {
    pick: 'Skin banner',
    change: 'Change',
    remove: 'Remove',
    empty: 'Choose a skin',
    chooseChampion: 'Choose a champion',
    chooseSkin: 'Choose a skin',
    back: 'Back',
    close: 'Close',
    search: 'Search…',
    loading: 'Loading…',
    error: 'Error',
    retry: 'Retry',
    noResults: 'No results',
}

function mountPicker(current: { id: string; name: string; banner: string } | null) {
    return mount(SkinBannerPicker, {
        props: {
            fieldName: 'favoriteSkinId',
            current,
            championsEndpoint: '/api/picker/champions',
            skinsEndpoint: '/api/picker/skins',
            version: '16.14.1',
            lang: 'en_US',
            labels: LABELS,
        },
        attachTo: document.body,
    })
}

afterEach(() => {
    vi.restoreAllMocks()
})

describe('SkinBannerPicker', () => {
    it('seeds the hidden input from the server-resolved current banner', () => {
        const wrapper = mountPicker({ id: 'Ahri_7', name: 'Spirit Blossom Ahri', banner: '/cdn/ahri_7.jpg' })

        const input = wrapper.get('input[type="hidden"]')
        expect(input.attributes('name')).toBe('favoriteSkinId')
        expect((input.element as HTMLInputElement).value).toBe('Ahri_7')
        expect(wrapper.text()).toContain('Spirit Blossom Ahri')
    })

    it('starts empty with a blank hidden input when no banner is set', () => {
        const wrapper = mountPicker(null)

        expect((wrapper.get('input[type="hidden"]').element as HTMLInputElement).value).toBe('')
        expect(wrapper.get('.skin-socket').classes()).toContain('skin-socket--empty')
    })

    it('walks champion → skin and writes the composed id, then clears on remove', async () => {
        vi.stubGlobal(
            'fetch',
            vi.fn(async (url: string) => {
                const body = url.includes('/skins')
                    ? { skins: [{ id: 'Ahri_1', num: 1, name: 'Midnight Ahri', image: null, banner: '/cdn/ahri_1.jpg' }] }
                    : { options: [{ id: 'Ahri', name: 'Ahri', image: null }] }
                return { ok: true, json: async () => body } as Response
            }),
        )
        HTMLDialogElement.prototype.showModal = vi.fn()
        HTMLDialogElement.prototype.close = vi.fn()

        const wrapper = mountPicker(null)
        await wrapper.get('.skin-socket').trigger('click')
        await flush()

        await wrapper.get('.picker-option').trigger('click') // pick champion Ahri
        await flush()

        await wrapper.get('.skin-tile').trigger('click') // pick Midnight Ahri
        expect((wrapper.get('input[type="hidden"]').element as HTMLInputElement).value).toBe('Ahri_1')

        // Re-open and remove.
        await wrapper.get('.skin-socket').trigger('click')
        await flush()
        await wrapper.get('.picker-remove').trigger('click')
        expect((wrapper.get('input[type="hidden"]').element as HTMLInputElement).value).toBe('')
    })
})

/** Let the awaited fetch + nextTick microtasks settle. */
async function flush(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0))
}
