import { mount, flushPromises } from '@vue/test-utils'
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import ResourceLoader from './ResourceLoader.vue'
import { BUILD_WARM_PATH, requestWarm } from '../loader/warmBridge'

const props = {
    eyebrow: 'Data Dragon',
    title: 'Summoning data',
    subtitle: 'Fetching…',
    preparing: 'Preparing…',
    status: { fetching: 'fetching', ready: 'ready' },
    labels: { champions: 'Champions', items: 'Items', runes: 'Runes', summoners: 'Summoner Spells' },
}

/** In-memory EventSource the component opens against /api/loader/prepare. */
class FakeEventSource {
    static instances = 0
    static last: FakeEventSource | null = null
    url: string
    closed = false
    onerror: (() => void) | null = null
    private listeners: Record<string, ((e: { data: string }) => void)[]> = {}
    constructor(url: string) {
        this.url = url
        FakeEventSource.instances++
        FakeEventSource.last = this
    }
    addEventListener(type: string, cb: (e: { data: string }) => void): void {
        ;(this.listeners[type] ??= []).push(cb)
    }
    emit(type: string, data: unknown): void {
        ;(this.listeners[type] ?? []).forEach((cb) => cb({ data: JSON.stringify(data) }))
    }
    fail(): void {
        this.onerror?.()
    }
    close(): void {
        this.closed = true
    }
}

const mountLoader = () => mount(ResourceLoader, { props })

/** Dispatch a cancelable before-visit and hand the event back so specs can read defaultPrevented. */
function beforeVisit(url: string): CustomEvent {
    const e = new CustomEvent('turbo:before-visit', { detail: { url }, cancelable: true })
    document.dispatchEvent(e)
    return e
}
const load = () => document.dispatchEvent(new CustomEvent('turbo:load'))

/** Dispatch the header version/language switcher submit the composable listens for. */
function switchSubmit(version: string, lang: string, action = '/setup-submit'): void {
    const form = document.createElement('form')
    form.setAttribute('action', action)
    const v = document.createElement('input'); v.name = 'version'; v.value = version
    const l = document.createElement('input'); l.name = 'langue'; l.value = lang
    form.append(v, l)
    document.body.appendChild(form)
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    form.remove()
}

let visit: ReturnType<typeof vi.fn>

describe('ResourceLoader', () => {
    beforeEach(() => {
        vi.useFakeTimers()
        FakeEventSource.instances = 0
        FakeEventSource.last = null
        // @ts-expect-error test double
        globalThis.EventSource = FakeEventSource
        // Turbo.visit mirrors the real thing: it re-fires before-visit synchronously,
        // so the bypass (re-entry) guard is exercised.
        visit = vi.fn((url: string) => beforeVisit(url))
        // @ts-expect-error partial Turbo
        window.Turbo = { visit }
        // Switcher persists prefs via fetch; the response is never inspected.
        globalThis.fetch = vi.fn().mockResolvedValue(undefined) as unknown as typeof fetch
    })
    afterEach(() => {
        vi.useRealTimers()
        vi.restoreAllMocks()
        window.history.replaceState({}, '', '/')
    })

    it('intercepts a cold resource visit, streams real progress + names, then performs the warm visit', async () => {
        const w = mountLoader()
        const e = beforeVisit('/champions?version=15.1.1&lang=en_US')
        expect(e.defaultPrevented).toBe(true)
        expect(FakeEventSource.last).not.toBeNull()

        const es = FakeEventSource.last!
        // The overlay surfaces only once `start` reports images to warm (total > 0).
        es.emit('start', { total: 2, categories: { champion: 2 } })
        await w.vm.$nextTick()
        expect(w.vm.visible).toBe(true)

        es.emit('item', { name: 'Aatrox', category: 'champion', index: 1, total: 2 })
        es.emit('item', { name: 'Ahri', category: 'champion', index: 2, total: 2 })
        await w.vm.$nextTick()

        expect(w.vm.progress).toBe(1)
        expect(w.vm.entries.map((x: { name: string }) => x.name)).toEqual(['Aatrox', 'Ahri'])
        // The live line surfaces only the resource landing right now (the latest).
        expect(w.text()).toContain('Ahri')

        es.emit('done', { stored: 2, total: 2 })
        expect(w.vm.finishing).toBe(true)
        expect(es.closed).toBe(true)

        vi.advanceTimersByTime(500)
        expect(visit).toHaveBeenCalledWith('/champions?version=15.1.1&lang=en_US')
        expect(visit).toHaveBeenCalledTimes(1)
        // The gated (bypassed) visit must not spawn a second stream.
        expect(FakeEventSource.instances).toBe(1)
        w.unmount()
    })

    it('never surfaces the overlay on a warm destination (total 0), whatever the latency', async () => {
        const w = mountLoader()
        beforeVisit('/objects?version=15.1.1&lang=en_US')
        const es = FakeEventSource.last!
        es.emit('start', { total: 0, categories: { item: 0 } })
        await w.vm.$nextTick()

        // A slow round-trip between start and done (dev boot / slow network) must NOT
        // surface the overlay: warmth is decided by total, never by elapsed time.
        vi.advanceTimersByTime(3000)
        expect(w.vm.visible).toBe(false)

        es.emit('done', { stored: 0, total: 0 })
        await w.vm.$nextTick()
        expect(w.vm.visible).toBe(false)
        expect(visit).toHaveBeenCalledWith('/objects?version=15.1.1&lang=en_US')
        w.unmount()
    })

    it('lets destinations that load no image batch navigate normally', async () => {
        const w = mountLoader()
        const e = beforeVisit('/working-progress')
        expect(e.defaultPrevented).toBe(false)
        expect(FakeEventSource.instances).toBe(0)
        w.unmount()
    })

    it('skips the gate when version/lang are unknown (falls through to a normal visit)', async () => {
        const w = mountLoader()
        const e = beforeVisit('/champions') // no query, no dd-* meta in jsdom head
        expect(e.defaultPrevented).toBe(false)
        expect(FakeEventSource.instances).toBe(0)
        w.unmount()
    })

    it('falls back to a normal visit when the stream errors', async () => {
        const w = mountLoader()
        beforeVisit('/runes?version=15.1.1&lang=en_US')
        const es = FakeEventSource.last!
        es.fail()
        expect(es.closed).toBe(true)
        expect(visit).toHaveBeenCalledWith('/runes?version=15.1.1&lang=en_US')
        expect(w.vm.visible).toBe(false)
        w.unmount()
    })

    it('names the four home resources and marks each ready as its group completes', async () => {
        const w = mountLoader()
        beforeVisit('/?version=15.1.1&lang=en_US')
        vi.advanceTimersByTime(300)
        await w.vm.$nextTick()
        expect(w.vm.active).toEqual(['champions', 'items', 'runes', 'summoners'])
        expect(w.findAll('.hx-row')).toHaveLength(4)

        const es = FakeEventSource.last!
        es.emit('start', { total: 4, categories: { champion: 1, item: 1, runesReforged: 1, summoner: 1 } })
        es.emit('item', { name: 'Aatrox', category: 'champion', index: 1, total: 4 })
        await w.vm.$nextTick()
        // Champions group (total 1) is now complete → row flips to ready.
        expect(w.text()).toContain('ready')

        es.emit('done', { stored: 4, total: 4 })
        vi.advanceTimersByTime(500)
        expect(visit).toHaveBeenCalledWith('/?version=15.1.1&lang=en_US')
        w.unmount()
    })

    it('rearms the inactivity watchdog on each event so a long but progressing warm is not cut', async () => {
        const w = mountLoader()
        beforeVisit('/champions?version=15.1.1&lang=en_US')
        vi.advanceTimersByTime(300)
        const es = FakeEventSource.last!
        es.emit('start', { total: 3, categories: { champion: 3 } })

        // Three items, each 12s after the previous — 36s total, well past the old
        // 15s absolute cap, but never 15s of silence. Must not trigger a visit.
        for (let i = 1; i <= 3; i++) {
            vi.advanceTimersByTime(12000)
            es.emit('item', { name: 'Champ' + i, category: 'champion', index: i, total: 3 })
        }
        expect(visit).not.toHaveBeenCalled()
        expect(es.closed).toBe(false)

        es.emit('done', { stored: 3, total: 3 })
        vi.advanceTimersByTime(500)
        expect(visit).toHaveBeenCalledWith('/champions?version=15.1.1&lang=en_US')
        w.unmount()
    })

    it('still gives up when the stream goes silent past the idle window', async () => {
        const w = mountLoader()
        beforeVisit('/champions?version=15.1.1&lang=en_US')
        const es = FakeEventSource.last!
        es.emit('start', { total: 5, categories: { champion: 5 } })

        // No further events: after WATCHDOG_IDLE of silence, bail out to a visit.
        vi.advanceTimersByTime(15000)
        expect(es.closed).toBe(true)
        vi.advanceTimersByTime(500) // ready-hold before the gated visit
        expect(visit).toHaveBeenCalledWith('/champions?version=15.1.1&lang=en_US')
        w.unmount()
    })

    it('hides once the warm destination has loaded', async () => {
        const w = mountLoader()
        beforeVisit('/champions?version=15.1.1&lang=en_US')
        vi.advanceTimersByTime(300)
        const es = FakeEventSource.last!
        es.emit('start', { total: 1, categories: { champion: 1 } })
        es.emit('done', { stored: 1, total: 1 })
        vi.advanceTimersByTime(500)
        await w.vm.$nextTick()
        expect(w.vm.visible).toBe(true) // still showing "ready" over the (mocked) warm swap

        load()
        await w.vm.$nextTick()
        expect(w.vm.visible).toBe(false)
        w.unmount()
    })

    it('gates a version/language switch from a batch-less page and covers the cold reload without streaming', async () => {
        // On a detail page (no image batch to stream) the switch must still show the
        // loader — raised instantly, then a straight visit under the new version.
        window.history.replaceState({}, '', '/champion/Ahri')
        const w = mountLoader()

        switchSubmit('15.1.1', 'en_US')
        await w.vm.$nextTick()
        expect(w.vm.visible).toBe(true) // overlay up before the prefs round-trip resolves
        expect(globalThis.fetch).toHaveBeenCalledTimes(1)

        await flushPromises()
        // Batch-less destination → no SSE stream, straight to the versioned visit.
        expect(FakeEventSource.instances).toBe(0)
        expect(visit).toHaveBeenCalledWith('/15.1.1/champion/Ahri?lang=en_US')
        w.unmount()
    })

    it('gates a switch that lands on a list page: streams the batch then visits', async () => {
        window.history.replaceState({}, '', '/champions')
        const w = mountLoader()

        switchSubmit('15.1.1', 'en_US')
        await w.vm.$nextTick()
        expect(w.vm.visible).toBe(true) // eager overlay
        await flushPromises() // prefs POST settles → startPrepare opens the SSE
        expect(FakeEventSource.instances).toBe(1)

        const es = FakeEventSource.last!
        es.emit('start', { total: 1, categories: { champion: 1 } })
        es.emit('item', { name: 'Aatrox', category: 'champion', index: 1, total: 1 })
        es.emit('done', { stored: 1, total: 1 })
        vi.advanceTimersByTime(500)
        expect(visit).toHaveBeenCalledWith('/15.1.1/champions?lang=en_US')
        w.unmount()
    })

    it('warms a cold patch in place on request, streaming real progress, then resumes WITHOUT navigating', async () => {
        window.history.replaceState({}, '', '/builds/new')
        const w = mountLoader()

        let resolved = false
        void requestWarm('15.1.1', 'en_US', BUILD_WARM_PATH).then(() => { resolved = true })
        await w.vm.$nextTick()

        // The stream targets the build warm token and the manifest names its three catalogs.
        const es = FakeEventSource.last!
        expect(FakeEventSource.instances).toBe(1)
        expect(es.url).toContain('path=%2Fbuilds%2Feditor')
        expect(w.vm.active).toEqual(['champions', 'items', 'runes'])

        es.emit('start', { total: 2, categories: { champion: 1, item: 1 } })
        await w.vm.$nextTick()
        expect(w.vm.visible).toBe(true)
        es.emit('item', { name: 'Aatrox', category: 'champion', index: 1, total: 2 })
        es.emit('item', { name: 'Boots', category: 'item', index: 2, total: 2 })
        es.emit('done', { stored: 2, total: 2 })

        vi.advanceTimersByTime(500) // ready-hold, then hand back
        await flushPromises()
        expect(resolved).toBe(true)
        expect(w.vm.visible).toBe(false)
        expect(visit).not.toHaveBeenCalled() // in-place warm never performs a Turbo visit
        w.unmount()
    })

    it('resumes a warm patch immediately without ever surfacing the overlay (total 0)', async () => {
        window.history.replaceState({}, '', '/builds/new')
        const w = mountLoader()

        let resolved = false
        void requestWarm('15.1.1', 'en_US', BUILD_WARM_PATH).then(() => { resolved = true })
        await w.vm.$nextTick()

        const es = FakeEventSource.last!
        es.emit('start', { total: 0, categories: { champion: 0, item: 0, runesReforged: 0 } })
        es.emit('done', { stored: 0, total: 0 })
        await flushPromises()

        expect(resolved).toBe(true)
        expect(w.vm.visible).toBe(false)
        expect(visit).not.toHaveBeenCalled()
        w.unmount()
    })

    it('resolves the warm request immediately when no loader island is mounted', async () => {
        let resolved = false
        await requestWarm('15.1.1', 'en_US', BUILD_WARM_PATH).then(() => { resolved = true })
        expect(resolved).toBe(true)
        expect(FakeEventSource.instances).toBe(0)
    })
})
