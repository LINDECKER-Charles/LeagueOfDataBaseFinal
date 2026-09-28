import { afterEach, describe, expect, it, vi } from 'vitest'
import { syncLocation } from './turboLocation'

/**
 * The URL rewrite must go through Turbo when it is there (snapshot cache keyed
 * by the location Turbo last rendered), and degrade to a plain replaceState
 * otherwise — never a navigation.
 */
type TurboWindow = Window & { Turbo?: unknown }

afterEach(() => {
    delete (window as TurboWindow).Turbo
    vi.restoreAllMocks()
})

describe('syncLocation', () => {
    it('rewrites through Turbo and re-keys its last rendered location', () => {
        const replace = vi.fn()
        const view: { lastRenderedLocation?: URL } = {}
        ;(window as TurboWindow).Turbo = {
            session: { history: { replace, restorationIdentifier: 'abc' }, view },
        }

        syncLocation('/objects?tag=Boots')

        expect(replace).toHaveBeenCalledTimes(1)
        expect(replace.mock.calls[0][0]).toBeInstanceOf(URL)
        expect(replace.mock.calls[0][0].pathname + replace.mock.calls[0][0].search).toBe('/objects?tag=Boots')
        expect(replace.mock.calls[0][1]).toBe('abc')
        expect(view.lastRenderedLocation?.search).toBe('?tag=Boots')
    })

    it('falls back to history.replaceState, keeping the current state object', () => {
        const replaceState = vi.spyOn(window.history, 'replaceState')

        syncLocation('/objects?q=sword')

        expect(replaceState).toHaveBeenCalledTimes(1)
        expect(replaceState.mock.calls[0][0]).toBe(window.history.state)
        expect(String(replaceState.mock.calls[0][2])).toContain('/objects?q=sword')
    })
})
