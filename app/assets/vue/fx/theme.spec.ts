import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { installTheme } from './theme'

/**
 * The picker is the only piece of the theme chain that runs in the browser: the
 * server already renders `data-theme` and the chrome meta. What these tests pin
 * is therefore what the server cannot do — swapping the attribute on choice,
 * persisting it in a cookie the server will read back, closing the dialog every
 * way it can be closed, and re-asserting the cookie on `turbo:load`, which is
 * what heals a page replayed from the service-worker cache or a theme switched
 * in another tab.
 */
describe('theme picker', () => {
    const root = document.documentElement
    const THEMES = ['hextech', 'zaun', 'noxus', 'spirit-blossom']
    const CHROME: Record<string, string> = {
        hextech: '#010a13',
        zaun: '#030706',
        noxus: '#08090b',
        'spirit-blossom': '#07070e',
    }

    const render = (current: string): void => {
        document.body.innerHTML = `
            <button data-theme-open>open</button>
            <dialog data-theme-dialog>
              <button data-theme-close>close</button>
              <div>
                ${THEMES.map((t) => `
                  <button data-theme-option="${t}" data-theme-chrome="${CHROME[t]}"
                          aria-pressed="${t === current}"><svg></svg></button>`).join('')}
              </div>
            </dialog>`
    }

    const dialog = (): HTMLDialogElement =>
        document.querySelector<HTMLDialogElement>('[data-theme-dialog]')!

    const click = (selector: string): void => {
        document.querySelector(selector)!.dispatchEvent(new MouseEvent('click', { bubbles: true }))
    }

    const pressed = (): Record<string, string | null> =>
        Object.fromEntries(
            [...document.querySelectorAll<HTMLElement>('[data-theme-option]')].map((option) => [
                option.dataset.themeOption!,
                option.getAttribute('aria-pressed'),
            ]),
        )

    beforeEach(() => {
        // jsdom implements <dialog> but not showModal's focus/inertness; stub the
        // two methods so the module's calls are observable and `open` stays true
        // to what a browser would report.
        HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) {
            this.open = true
        })
        HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) {
            this.open = false
        })
        document.head.innerHTML = '<meta name="theme-color" content="#010a13">'
        root.dataset.theme = 'hextech'
        document.cookie = 'lod_theme=; max-age=0; path=/'
        installTheme() // document-level listeners; same fn ref → deduped across calls
    })

    afterEach(() => {
        document.body.innerHTML = ''
        document.head.innerHTML = ''
        document.cookie = 'lod_theme=; max-age=0; path=/'
        vi.restoreAllMocks()
    })

    it('opens the dialog from the trigger', () => {
        render('hextech')

        click('[data-theme-open]')

        expect(dialog().open).toBe(true)
    })

    it.each(['zaun', 'noxus', 'spirit-blossom'])(
        'applies %s, persists it, and closes',
        (name) => {
            render('hextech')
            click('[data-theme-open]')

            // Click the inner <svg>, as a real pointer does — the handler walks up.
            document
                .querySelector(`[data-theme-option="${name}"] svg`)!
                .dispatchEvent(new MouseEvent('click', { bubbles: true }))

            expect(root.dataset.theme).toBe(name)
            expect(document.cookie).toContain(`lod_theme=${name}`)
            expect(pressed()[name]).toBe('true')
            expect(document.querySelector('meta[name="theme-color"]')?.getAttribute('content'))
                .toBe(CHROME[name])
            expect(dialog().open).toBe(false)
        },
    )

    it('marks exactly one option as pressed', () => {
        render('hextech')
        click('[data-theme-option="noxus"]')

        expect(Object.values(pressed()).filter((v) => v === 'true')).toHaveLength(1)
    })

    it('closes from the close button and from the backdrop', () => {
        render('hextech')

        click('[data-theme-open]')
        click('[data-theme-close]')
        expect(dialog().open).toBe(false)

        click('[data-theme-open]')
        // A click whose target IS the <dialog> element landed on its backdrop:
        // every piece of content sits in a child.
        dialog().dispatchEvent(new MouseEvent('click', { bubbles: true }))
        expect(dialog().open).toBe(false)
    })

    it('ignores clicks that are not on an option', () => {
        render('hextech')
        document.body.insertAdjacentHTML('beforeend', '<button id="other">x</button>')

        click('#other')

        expect(root.dataset.theme).toBe('hextech')
    })

    it('re-asserts the cookie on a Turbo visit, healing a stale cached page', () => {
        document.cookie = 'lod_theme=noxus; path=/'
        // A page replayed from the SW cache carries the identity it was cached with.
        root.dataset.theme = 'hextech'
        render('hextech')

        document.dispatchEvent(new Event('turbo:load'))

        expect(root.dataset.theme).toBe('noxus')
        expect(pressed().noxus).toBe('true')
    })

    it('falls back to the default identity when the cookie is unrecognised', () => {
        document.cookie = 'lod_theme=demacia; path=/'
        root.dataset.theme = 'zaun'
        render('zaun')

        document.dispatchEvent(new Event('turbo:load'))

        expect(root.dataset.theme).toBe('hextech')
    })
})
