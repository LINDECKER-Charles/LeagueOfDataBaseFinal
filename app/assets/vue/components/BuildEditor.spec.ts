import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { clearPickerCatalogCache } from '../builds/usePickerCatalog'
import BuildEditor from './BuildEditor.vue'

/**
 * Contract of the island with its Twig host: the game-context selects are REAL
 * form fields (game_version / game_mode), the hidden `structure` input mirrors
 * the state, and switching mode/version re-fetches the catalogs — flagging
 * already-placed items that the new context no longer offers (never removing).
 */
const VERSION = '16.14.1'
const OLD_VERSION = '16.13.1'
const LANG = 'en_US'

const GREAVES = { id: '3006', name: 'Berserker Greaves', image: null, gold: 1100, purchasable: true, tags: [] }
const DORANS = { id: '1055', name: "Doran's Blade", image: null, gold: 450, purchasable: true, tags: [] }

function payloadFor(url: string): unknown {
    if (url.includes('/champions')) {
        return { options: [{ id: 'Aatrox', key: '266', name: 'Aatrox', image: null }] }
    }
    if (url.includes('/runes')) {
        return { trees: [] }
    }
    const mode = new URL(url, 'http://test.local').searchParams.get('mode')
    // Greaves exist on Summoner's Rift only — the ARAM catalog drops them.
    return { options: mode === 'sr' ? [GREAVES, DORANS] : [DORANS] }
}

const labels = {
    loading: 'Loading…',
    error: 'Unavailable.',
    retry: 'Retry',
    ghost: 'Unavailable on this patch',
    ghostMode: 'Not available in this game mode',
    counter: '%count% / %max%',
    context: { title: 'Game context', version: 'Patch', mode: 'Game mode', modeHint: 'Availability follows the mode.', language: 'Authoring language' },
    dnd: {
        handle: 'Drag to reorder',
        movedStep: 'Step moved to position %position%',
        movedItem: 'Item moved to position %position%',
        transferred: 'Item moved to step %step%',
        added: 'Item added to step %step%',
        cancelled: 'Move cancelled',
    },
    champion: { title: 'Champion', search: 'Search…', empty: 'None.', selected: 'Chosen', open: 'Choose', close: 'Close' },
    runes: {
        title: 'Runes',
        primary: 'Primary',
        secondary: 'Secondary',
        keystone: 'Keystone',
        slot: 'Row %n%',
        secondaryHint: 'Pick 2.',
    },
    steps: {
        title: 'Purchase order',
        add: 'Add a step',
        remove: 'Remove step',
        moveUp: 'Up',
        moveDown: 'Down',
        label: 'Step label',
        note: 'Note',
        searchItem: 'Search an item…',
        itemEmpty: 'None.',
        removeItem: 'Remove item',
        gold: 'Step cost',
        presets: ['Start'],
    },
    armory: {
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
    },
}

function mountEditor() {
    return mount(BuildEditor, {
        props: {
            mode: 'edit' as const,
            initial: {
                championId: 'Aatrox',
                runes: { primaryStyleId: 0, primarySelections: [], secondaryStyleId: 0, secondarySelections: [] },
                steps: [{ label: 'Start', note: null, items: ['3006'] }],
            },
            endpoints: { champions: '/api/picker/champions', items: '/api/picker/items', runes: '/api/picker/runes' },
            version: VERSION,
            versions: [VERSION, OLD_VERSION],
            lang: LANG,
            gameMode: 'sr',
            gameModes: [
                { value: 'sr', label: "Summoner's Rift" },
                { value: 'aram', label: 'ARAM' },
            ],
            language: LANG,
            languages: [
                { value: 'en_US', label: 'English (US)' },
                { value: 'fr_FR', label: 'French' },
            ],
            labels,
        },
    })
}

function fetchedUrls(fetchMock: ReturnType<typeof vi.fn>): string[] {
    return fetchMock.mock.calls.map(([url]) => String(url))
}

describe('BuildEditor island', () => {
    let fetchMock: ReturnType<typeof vi.fn>

    beforeEach(() => {
        clearPickerCatalogCache()
        fetchMock = vi.fn(async (url: string) => ({ ok: true, json: async () => payloadFor(String(url)) }))
        vi.stubGlobal('fetch', fetchMock)
    })

    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('renders the game-context selects as real form fields', async () => {
        const wrapper = mountEditor()
        await flushPromises()

        const version = wrapper.find('select[name="game_version"]')
        const mode = wrapper.find('select[name="game_mode"]')
        expect((version.element as HTMLSelectElement).value).toBe(VERSION)
        expect((mode.element as HTMLSelectElement).value).toBe('sr')
        expect(wrapper.find('input[name="structure"]').attributes('value')).toContain('"championId":"Aatrox"')
    })

    it('loads the three catalogs for the initial (version, mode) context', async () => {
        mountEditor()
        await flushPromises()

        const urls = fetchedUrls(fetchMock)
        expect(urls).toHaveLength(3)
        expect(urls.some((u) => u.includes(`/champions?version=${VERSION}&lang=${LANG}`))).toBe(true)
        expect(urls.some((u) => u.includes(`/items?version=${VERSION}&lang=${LANG}&mode=sr`))).toBe(true)
    })

    it('re-fetches items on mode switch and flags now-unavailable placed items', async () => {
        const wrapper = mountEditor()
        await flushPromises()
        expect(wrapper.find('.forge-ghost').exists()).toBe(false)

        await wrapper.find('select[name="game_mode"]').setValue('aram')
        await flushPromises()

        expect(fetchedUrls(fetchMock).some((u) => u.includes('&mode=aram'))).toBe(true)
        const ghost = wrapper.find('.forge-ghost')
        expect(ghost.exists()).toBe(true)
        // Identity survives (name from the previously seen catalog) + honest reason.
        expect(ghost.attributes('title')).toBe(`Berserker Greaves — ${labels.ghostMode}`)
    })

    it('re-fetches every catalog on version switch', async () => {
        const wrapper = mountEditor()
        await flushPromises()
        fetchMock.mockClear()

        await wrapper.find('select[name="game_version"]').setValue(OLD_VERSION)
        await flushPromises()

        const urls = fetchedUrls(fetchMock)
        expect(urls).toHaveLength(3)
        expect(urls.every((u) => u.includes(`version=${OLD_VERSION}`))).toBe(true)
    })

    it('exposes the authoring language as a real form field that never reloads catalogs', async () => {
        const wrapper = mountEditor()
        await flushPromises()
        fetchMock.mockClear()

        const language = wrapper.find('select[name="language"]')
        expect((language.element as HTMLSelectElement).value).toBe(LANG)

        await language.setValue('fr_FR')
        await flushPromises()

        expect((language.element as HTMLSelectElement).value).toBe('fr_FR')
        // Authoring language is metadata, not a catalog axis: no refetch on change.
        expect(fetchedUrls(fetchMock)).toHaveLength(0)
    })
})
