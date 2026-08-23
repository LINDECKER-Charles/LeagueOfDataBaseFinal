import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { installTheme } from './theme'

/**
 * The toggle is the only piece of the theme chain that runs in the browser: the
 * server already renders `data-theme` and the chrome meta. What these tests pin
 * is therefore the three things the server cannot do — swapping the attribute on
 * click, persisting the choice in a cookie the server will read back, and
 * re-asserting that cookie on `turbo:load`, which is what heals a page replayed
 * from the service-worker cache or a theme switched in another tab.
 */
describe('theme toggle', () => {
    const root = document.documentElement

    const renderToggle = (current: 'hextech' | 'zaun'): void => {
        document.body.innerHTML = `
            <div role="group">
              <button data-theme-option="hextech" data-theme-chrome="#010a13"
                      aria-pressed="${current === 'hextech'}"><svg></svg></button>
              <button data-theme-option="zaun" data-theme-chrome="#030706"
                      aria-pressed="${current === 'zaun'}"><svg></svg></button>
            </div>`
    }

    const clickOption = (name: string): void => {
        // Click the inner <svg>, as a real pointer does — the handler has to walk up.
        document
            .querySelector(`[data-theme-option="${name}"] svg`)!
            .dispatchEvent(new MouseEvent('click', { bubbles: true }))
    }

    const pressedState = (): Record<string, string | null> =>
        Object.fromEntries(
            [...document.querySelectorAll<HTMLElement>('[data-theme-option]')].map((option) => [
                option.dataset.themeOption!,
                option.getAttribute('aria-pressed'),
            ]),
        )

    beforeEach(() => {
        document.head.innerHTML = '<meta name="theme-color" content="#010a13">'
        root.dataset.theme = 'hextech'
        document.cookie = 'lod_theme=; max-age=0; path=/'
        installTheme() // document-level listeners; same fn ref → deduped across calls
    })

    afterEach(() => {
        document.body.innerHTML = ''
        document.head.innerHTML = ''
        document.cookie = 'lod_theme=; max-age=0; path=/'
    })

    it('swaps the identity, the pressed state and the browser chrome on click', () => {
        renderToggle('hextech')

        clickOption('zaun')

        expect(root.dataset.theme).toBe('zaun')
        expect(pressedState()).toEqual({ hextech: 'false', zaun: 'true' })
        expect(document.querySelector('meta[name="theme-color"]')?.getAttribute('content'))
            .toBe('#030706')
    })

    it('persists the choice where the server reads it back', () => {
        renderToggle('hextech')

        clickOption('zaun')

        expect(document.cookie).toContain('lod_theme=zaun')
    })

    it('ignores clicks that are not on an option', () => {
        renderToggle('hextech')
        document.body.insertAdjacentHTML('beforeend', '<button id="other">x</button>')

        document.querySelector('#other')!.dispatchEvent(new MouseEvent('click', { bubbles: true }))

        expect(root.dataset.theme).toBe('hextech')
    })

    it('re-asserts the cookie on a Turbo visit, healing a stale cached page', () => {
        document.cookie = 'lod_theme=zaun; path=/'
        // A page replayed from the SW cache carries the identity it was cached with.
        root.dataset.theme = 'hextech'
        renderToggle('hextech')

        document.dispatchEvent(new Event('turbo:load'))

        expect(root.dataset.theme).toBe('zaun')
        expect(pressedState()).toEqual({ hextech: 'false', zaun: 'true' })
    })

    it('falls back to the default identity when the cookie is unrecognised', () => {
        document.cookie = 'lod_theme=noxus; path=/'
        root.dataset.theme = 'zaun'
        renderToggle('zaun')

        document.dispatchEvent(new Event('turbo:load'))

        expect(root.dataset.theme).toBe('hextech')
    })
})
