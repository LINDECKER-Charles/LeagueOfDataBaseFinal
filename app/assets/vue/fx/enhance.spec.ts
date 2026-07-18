import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { installEnhancements } from './enhance'

/**
 * Regression: under Turbo Drive, clicking a [data-scrollspy] section-nav anchor
 * on a URL that carries a ?version&lang query used to trigger a full Drive visit
 * (perceived reload) instead of scrolling. installEnhancements() must own the
 * click and scroll in place.
 */
describe('section-nav in-page anchor', () => {
    const scrollIntoView = vi.fn()

    beforeEach(() => {
        vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false })))
        Element.prototype.scrollIntoView = scrollIntoView as unknown as typeof Element.prototype.scrollIntoView
        installEnhancements() // document-level listeners; same fn ref → deduped across calls
    })

    afterEach(() => {
        document.body.innerHTML = ''
        vi.unstubAllGlobals()
        vi.clearAllMocks()
    })

    const click = (el: Element): MouseEvent => {
        const event = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 })
        el.dispatchEvent(event)
        return event
    }

    it('scrolls to the target and updates the URL rather than navigating', () => {
        document.body.innerHTML =
            '<nav data-scrollspy><a href="#abilities">A</a></nav><section id="abilities"></section>'
        const push = vi.spyOn(history, 'pushState')

        const event = click(document.querySelector('a[href="#abilities"]')!)

        expect(event.defaultPrevented).toBe(true)
        expect(scrollIntoView).toHaveBeenCalledWith(
            expect.objectContaining({ behavior: 'smooth', block: 'start' }),
        )
        expect(push.mock.calls[0].slice(1)).toEqual(['', '#abilities'])
    })

    it('falls back to auto scroll when reduced motion is preferred', () => {
        vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: true })))
        document.body.innerHTML =
            '<nav data-scrollspy><a href="#skins">S</a></nav><section id="skins"></section>'

        click(document.querySelector('a[href="#skins"]')!)

        expect(scrollIntoView).toHaveBeenCalledWith(expect.objectContaining({ behavior: 'auto' }))
    })

    it('leaves clicks outside a scrollspy nav to the browser', () => {
        document.body.innerHTML = '<a href="#abilities">A</a><section id="abilities"></section>'

        const event = click(document.querySelector('a')!)

        expect(event.defaultPrevented).toBe(false)
        expect(scrollIntoView).not.toHaveBeenCalled()
    })

    it('ignores modifier clicks so open-in-new-tab keeps working', () => {
        document.body.innerHTML =
            '<nav data-scrollspy><a href="#abilities">A</a></nav><section id="abilities"></section>'
        const link = document.querySelector('a[href="#abilities"]')!

        const event = new MouseEvent('click', { bubbles: true, cancelable: true, button: 0, metaKey: true })
        link.dispatchEvent(event)

        expect(event.defaultPrevented).toBe(false)
        expect(scrollIntoView).not.toHaveBeenCalled()
    })
})
