import { mount } from '@vue/test-utils'
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import ResourceLoader from './ResourceLoader.vue'

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
    })
    afterEach(() => {
        vi.useRealTimers()
        vi.restoreAllMocks()
    })

    it('intercepts a cold resource visit, streams real progress + names, then performs the warm visit', async () => {
        const w = mountLoader()
        const e = beforeVisit('/champions?version=15.1.1&lang=en_US')
        expect(e.defaultPrevented).toBe(true)
        expect(FakeEventSource.last).not.toBeNull()

        vi.advanceTimersByTime(300)
        await w.vm.$nextTick()
        expect(w.vm.visible).toBe(true)

        const es = FakeEventSource.last!
        es.emit('start', { total: 2, categories: { champion: 2 } })
        es.emit('item', { name: 'Aatrox', category: 'champion', index: 1, total: 2 })
        es.emit('item', { name: 'Ahri', category: 'champion', index: 2, total: 2 })
        await w.vm.$nextTick()

        expect(w.vm.progress).toBe(1)
        expect(w.vm.entries.map((x: { name: string }) => x.name)).toEqual(['Aatrox', 'Ahri'])
        expect(w.text()).toContain('Aatrox')

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

    it('never flashes on a warm destination (total 0) and visits immediately', async () => {
        const w = mountLoader()
        beforeVisit('/objects?version=15.1.1&lang=en_US')
        const es = FakeEventSource.last!
        es.emit('start', { total: 0, categories: { item: 0 } })
        es.emit('done', { stored: 0, total: 0 }) // resolves before the 280ms show delay
        await w.vm.$nextTick()

        expect(w.vm.visible).toBe(false)
        expect(visit).toHaveBeenCalledWith('/objects?version=15.1.1&lang=en_US')
        vi.advanceTimersByTime(400)
        expect(w.vm.visible).toBe(false)
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
        beforeVisit('/home?version=15.1.1&lang=en_US')
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
        expect(visit).toHaveBeenCalledWith('/home?version=15.1.1&lang=en_US')
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
})
