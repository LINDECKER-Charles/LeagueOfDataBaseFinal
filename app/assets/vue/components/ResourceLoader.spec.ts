import { mount } from '@vue/test-utils'
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import ResourceLoader from './ResourceLoader.vue'

const props = {
    eyebrow: 'Data Dragon',
    title: 'Summoning data',
    subtitle: 'Fetching…',
    status: { fetching: 'fetching', ready: 'ready' },
    labels: { champions: 'Champions', items: 'Items', runes: 'Runes', summoners: 'Summoner Spells' },
}

const visit = (url: string) =>
    document.dispatchEvent(new CustomEvent('turbo:visit', { detail: { url } }))
const load = () => document.dispatchEvent(new CustomEvent('turbo:load'))

describe('ResourceLoader', () => {
    beforeEach(() => vi.useFakeTimers())
    afterEach(() => vi.useRealTimers())

    it('stays hidden until the show delay elapses, then names the destination resources', async () => {
        const w = mount(ResourceLoader, { props })
        visit('/home')
        await w.vm.$nextTick()
        expect(w.get('.hx-loader').isVisible()).toBe(false)

        vi.advanceTimersByTime(300)
        await w.vm.$nextTick()
        const root = w.get('.hx-loader')
        expect(root.isVisible()).toBe(true)
        expect(root.attributes('aria-busy')).toBe('true')
        expect(w.findAll('.hx-row')).toHaveLength(4)
        expect(w.text()).toContain('Champions')
        expect(w.text()).toContain('Summoner Spells')
        w.unmount()
    })

    it('never flashes on a warm visit that resolves before the delay', async () => {
        const w = mount(ResourceLoader, { props })
        visit('/champions')
        vi.advanceTimersByTime(150)
        load()
        vi.advanceTimersByTime(400)
        await w.vm.$nextTick()
        expect(w.get('.hx-loader').isVisible()).toBe(false)
        w.unmount()
    })

    it('ignores destinations that load no resources', async () => {
        const w = mount(ResourceLoader, { props })
        visit('/working-progress')
        vi.advanceTimersByTime(400)
        await w.vm.$nextTick()
        expect(w.get('.hx-loader').isVisible()).toBe(false)
        w.unmount()
    })

    it('maps a single-resource destination (list, detail and redirect share it)', async () => {
        for (const url of ['/objects', '/object/1001', '/objects_redirect/1/8']) {
            const w = mount(ResourceLoader, { props })
            visit(url)
            vi.advanceTimersByTime(300)
            await w.vm.$nextTick()
            const rows = w.findAll('.hx-row')
            expect(rows).toHaveLength(1)
            expect(rows[0].text()).toContain('Items')
            expect(w.text()).not.toContain('Champions')
            w.unmount()
        }
    })

    it('flips to ready on turbo:load, then hides after the minimum visible window', async () => {
        const w = mount(ResourceLoader, { props })
        visit('/champions')
        vi.advanceTimersByTime(300)
        await w.vm.$nextTick()
        expect(w.get('.hx-loader').attributes('aria-busy')).toBe('true')
        expect(w.text()).toContain('fetching')

        load()
        await w.vm.$nextTick()
        expect(w.get('.hx-loader').attributes('aria-busy')).toBe('false')
        expect(w.text()).toContain('ready')
        expect(w.get('.hx-loader').isVisible()).toBe(true)

        vi.advanceTimersByTime(1000)
        await w.vm.$nextTick()
        expect(w.get('.hx-loader').isVisible()).toBe(false)
        w.unmount()
    })
})
