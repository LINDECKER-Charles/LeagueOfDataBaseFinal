import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import FavoritePicker from './FavoritePicker.vue'

/**
 * Contract of the island with its Twig host: the `slots` prop drives the four
 * sockets AND the hidden inputs the surrounding form submits. An unresolvable
 * stored id must round-trip untouched (the server owns the warning), never be
 * silently cleared by the island.
 */
function mountPicker() {
    return mount(FavoritePicker, {
        props: {
            slots: [
                {
                    type: 'champion' as const,
                    fieldName: 'favoriteChampionId',
                    typeLabel: 'Champion',
                    current: { id: 'Aatrox', name: 'Aatrox', image: '/cdn/blobs/aatrox.png' },
                    storedId: 'Aatrox',
                    emptyLabel: 'Choose',
                },
                {
                    type: 'item' as const,
                    fieldName: 'favoriteItemId',
                    typeLabel: 'Item',
                    current: null,
                    storedId: '999999', // stored but unresolvable on this patch
                    emptyLabel: 'Choose',
                },
                {
                    type: 'rune' as const,
                    fieldName: 'favoriteRuneId',
                    typeLabel: 'Rune',
                    current: null,
                    storedId: null,
                    emptyLabel: 'Choose',
                },
                {
                    type: 'summoner' as const,
                    fieldName: 'favoriteSummonerId',
                    typeLabel: 'Summoner spell',
                    current: null,
                    storedId: null,
                    emptyLabel: 'Choose',
                },
            ],
            endpoints: {
                champion: '/api/picker/champions',
                item: '/api/picker/items',
                rune: '/api/picker/runes',
                summoner: '/api/picker/summoners',
            },
            version: '16.14.1',
            lang: 'en_US',
            labels: {
                search: 'Search…',
                remove: 'Remove',
                close: 'Close',
                loading: 'Loading…',
                error: 'Error',
                retry: 'Retry',
                noResults: 'No results',
                unavailable: 'Unavailable on this patch',
            },
        },
    })
}

describe('FavoritePicker', () => {
    it('renders one socket per slot with its display state', () => {
        const wrapper = mountPicker()
        const sockets = wrapper.findAll('.socket')

        expect(sockets).toHaveLength(4)
        expect(sockets[0]!.text()).toContain('Aatrox')
        expect(sockets[0]!.classes()).toContain('socket--filled')
        expect(sockets[1]!.text()).toContain('Unavailable on this patch')
        expect(sockets[1]!.classes()).toContain('socket--unavailable')
        expect(sockets[2]!.text()).toContain('Choose')
        expect(sockets[2]!.classes()).toContain('socket--empty')
    })

    it('renders form hidden inputs, round-tripping unresolvable stored ids', () => {
        const wrapper = mountPicker()
        const values = Object.fromEntries(
            wrapper
                .findAll('input[type="hidden"]')
                .map((input) => [input.attributes('name'), (input.element as HTMLInputElement).value]),
        )

        expect(values).toEqual({
            favoriteChampionId: 'Aatrox',
            favoriteItemId: '999999',
            favoriteRuneId: '',
            favoriteSummonerId: '',
        })
    })
})
