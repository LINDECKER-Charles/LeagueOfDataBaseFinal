import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { installImageLifecycle, markImages } from './imageLifecycle'

/**
 * jsdom never loads image bytes: `complete` is true and `naturalWidth` 0 for
 * every <img>, which conveniently models "finished, nothing drawn". Loading
 * and loaded states are driven by overriding those getters per element.
 */
function img(attrs: string, opts: { complete?: boolean; width?: number } = {}): HTMLImageElement {
    document.body.insertAdjacentHTML('beforeend', `<img ${attrs}>`)
    const el = document.body.lastElementChild as HTMLImageElement
    Object.defineProperty(el, 'complete', { value: opts.complete ?? true, configurable: true })
    Object.defineProperty(el, 'naturalWidth', { value: opts.width ?? 0, configurable: true })
    return el
}

describe('image lifecycle', () => {
    beforeEach(() => installImageLifecycle())
    afterEach(() => {
        document.body.innerHTML = ''
    })

    it('stamps the state an image is already in at sweep time', () => {
        const loading = img('class="hx-img" src="/a.png"', { complete: false })
        const loaded = img('class="hx-img" src="/b.png"', { width: 56 })
        const failed = img('class="hx-img" src="/c.png"')
        const plain = img('src="/d.png"', { complete: false })

        markImages()

        expect(loading.dataset.imgState).toBe('loading')
        expect(loaded.dataset.imgState).toBe('loaded')
        expect(failed.dataset.imgState).toBe('error')
        expect(plain.dataset.imgState).toBeUndefined()
    })

    it('follows load and error events fired on the image itself', () => {
        const el = img('class="hx-img" src="/a.png"', { complete: false })
        markImages()

        el.dispatchEvent(new Event('load'))
        expect(el.dataset.imgState).toBe('loaded')

        el.dispatchEvent(new Event('error'))
        expect(el.dataset.imgState).toBe('error')
    })

    it('swaps a failed image to its fallback once and resumes loading', () => {
        const el = img('class="hx-img" src="/a.png" data-img-fallback="/b.png"', { complete: false })
        markImages()

        el.dispatchEvent(new Event('error'))

        expect(el.getAttribute('src')).toBe('/b.png')
        expect(el.hasAttribute('data-img-fallback')).toBe(false)
        expect(el.dataset.imgState).toBe('loading')

        el.dispatchEvent(new Event('error'))
        expect(el.getAttribute('src')).toBe('/b.png')
        expect(el.dataset.imgState).toBe('error')
    })

    it('applies the fallback to failures that happened before the sweep', () => {
        const el = img('src="/a.png" data-img-fallback="/b.png"')

        markImages()

        expect(el.getAttribute('src')).toBe('/b.png')
    })

    it('leaves already stamped images alone on a later sweep', () => {
        const el = img('class="hx-img" src="/a.png"', { complete: false })
        markImages()
        el.dispatchEvent(new Event('load'))

        markImages()

        expect(el.dataset.imgState).toBe('loaded')
    })
})
