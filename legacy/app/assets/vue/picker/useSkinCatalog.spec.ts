import { computed, nextTick } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useSkinCatalog } from './useSkinCatalog'

const CONFIG = {
    championsEndpoint: '/api/picker/champions',
    skinsEndpoint: '/api/picker/skins',
    version: '16.14.1',
    lang: 'en_US',
}

function stubSkinsFetch(): void {
    vi.stubGlobal(
        'fetch',
        vi.fn(async () => ({
            ok: true,
            json: async () => ({
                skins: [{ id: 'Ahri_1', num: 1, name: 'Midnight Ahri', image: null, banner: null }],
            }),
        })),
    )
}

afterEach(() => {
    vi.unstubAllGlobals()
})

describe('useSkinCatalog', () => {
    it('reads an unloaded champion as idle without registering it', () => {
        const catalog = useSkinCatalog(CONFIG)

        expect(catalog.skinsFor('Ahri').status).toBe('idle')
        // A read must stay a read: nothing was loaded, so nothing is pending.
        expect(catalog.skinsFor('Ahri').entries).toEqual([])
    })

    it('re-evaluates a computed reading skinsFor once the skins land', async () => {
        stubSkinsFetch()
        const catalog = useSkinCatalog(CONFIG)
        const status = computed(() => catalog.skinsFor('Ahri').status)
        expect(status.value).toBe('idle')

        await catalog.ensureSkins('Ahri')
        await nextTick()

        expect(status.value).toBe('ready')
        expect(catalog.skinsFor('Ahri').entries).toHaveLength(1)
    })

    it('memoises a loaded champion (a second ensure does not refetch)', async () => {
        stubSkinsFetch()
        const catalog = useSkinCatalog(CONFIG)

        await catalog.ensureSkins('Ahri')
        await catalog.ensureSkins('Ahri')

        expect(fetch).toHaveBeenCalledTimes(1)
    })
})
