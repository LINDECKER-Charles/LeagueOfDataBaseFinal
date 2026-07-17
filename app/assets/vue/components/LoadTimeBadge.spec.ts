import { mount } from '@vue/test-utils'
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import LoadTimeBadge from './LoadTimeBadge.vue'

const props = { labels: { title: 'Load time', server: 'Server', client: 'Client' } }

/** Controllable performance.now() clock (ms). */
let clock = 0

function addMarker(serverMs: string): void {
    const el = document.createElement('div')
    el.setAttribute('data-load-timing', '')
    el.dataset.serverMs = serverMs
    el.hidden = true
    document.body.appendChild(el)
}
function clearMarkers(): void {
    document.querySelectorAll('[data-load-timing]').forEach((e) => e.remove())
}

const beforeVisit = (): boolean => document.dispatchEvent(new CustomEvent('turbo:before-visit'))
const load = (): boolean => document.dispatchEvent(new CustomEvent('turbo:load'))
function fetchResponse(runtime: string | null): void {
    const headers = { get: (n: string): string | null => (n === 'X-Runtime' ? runtime : null) }
    document.dispatchEvent(
        new CustomEvent('turbo:before-fetch-response', { detail: { fetchResponse: { response: { headers } } } }),
    )
}

/** A Navigation Timing entry: DCL at `dcl`ms from navigation start. */
function navEntry(dcl: number): void {
    vi.spyOn(performance, 'getEntriesByType').mockReturnValue(
        [{ domContentLoadedEventEnd: dcl, responseEnd: dcl, startTime: 0 }] as unknown as PerformanceEntryList,
    )
}

describe('LoadTimeBadge', () => {
    beforeEach(() => {
        clock = 0
        vi.spyOn(performance, 'now').mockImplementation(() => clock)
        navEntry(0)
    })
    afterEach(() => {
        clearMarkers()
        vi.restoreAllMocks()
    })

    it('shows on a detail page (marker present) with server + client timings on the initial load', async () => {
        addMarker('42')
        navEntry(310) // hard load → client time from Navigation Timing
        const w = mount(LoadTimeBadge, { props })
        await w.vm.$nextTick() // onMounted flips visible after the first render

        expect(w.vm.visible).toBe(true)
        expect(w.vm.serverMs).toBe(42)
        expect(w.vm.clientMs).toBe(310)
        expect(w.text()).toContain('42 ms')
        expect(w.text()).toContain('310 ms')
        w.unmount()
    })

    it('stays hidden on a page without the marker (list / home)', () => {
        const w = mount(LoadTimeBadge, { props })
        expect(w.vm.visible).toBe(false)
        expect(w.find('.hx-perf').exists()).toBe(false)
        w.unmount()
    })

    it('measures a Turbo soft navigation from before-visit to load, preferring the X-Runtime header', async () => {
        const w = mount(LoadTimeBadge, { props }) // list page: no marker yet
        expect(w.vm.visible).toBe(false)

        clock = 1000
        beforeVisit() // navigation starts
        addMarker('42') // detail body swapped in (inline server ms)
        fetchResponse('55') // canonical server ms from the response header
        clock = 1350
        load()
        await w.vm.$nextTick()

        expect(w.vm.visible).toBe(true)
        expect(w.vm.serverMs).toBe(55) // header wins over the inline 42
        expect(w.vm.clientMs).toBe(350) // 1350 - 1000
        w.unmount()
    })

    it('falls back to the inline server ms when the response carries no X-Runtime header', async () => {
        const w = mount(LoadTimeBadge, { props })

        clock = 2000
        beforeVisit()
        addMarker('88')
        fetchResponse(null)
        clock = 2500
        load()
        await w.vm.$nextTick()

        expect(w.vm.serverMs).toBe(88)
        expect(w.vm.clientMs).toBe(500)
        w.unmount()
    })

    it('hides again when navigating from a detail page to a page without the marker', async () => {
        addMarker('30')
        const w = mount(LoadTimeBadge, { props })
        expect(w.vm.visible).toBe(true)

        clearMarkers() // soft-nav to a list page
        load()
        await w.vm.$nextTick()
        expect(w.vm.visible).toBe(false)
        w.unmount()
    })
})
