import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { startPendingImages, stopPendingImages } from './pendingImages'

/**
 * A cold grid: pending slots under a scope that carries the poll contract and
 * the template they turn into. IntersectionObserver is stubbed so the test
 * decides which slots are "near the viewport"; fetch answers the status poll.
 */
const SCOPE = `
<div data-img-scope="item" data-img-version="16.16.1" data-img-batch="2">
  <template data-img-template><div class="box"><picture>
    <source srcset="/__WEBP__" type="image/webp">
    <img src="/__SRC__" alt="" class="hx-img">
  </picture></div></template>
  <div id="grid">
    <div class="box hx-img-slot" data-img-pending data-img-name="1001.png" data-img-alt="Boots">BO</div>
    <div class="box hx-img-slot" data-img-pending data-img-name="2003.png" data-img-alt="Potion">PO</div>
    <div class="box hx-img-slot" data-img-pending data-img-name="3006.png" data-img-alt="Hidden">HI</div>
  </div>
</div>`

type Callback = (entries: Array<{ isIntersecting: boolean; target: Element }>) => void

let intersect: (targets: Element[]) => void
const fetchMock = vi.fn()

beforeEach(() => {
    vi.useFakeTimers()
    document.body.innerHTML = SCOPE
    let callback: Callback = () => {}
    const observed = new Set<Element>()
    vi.stubGlobal('IntersectionObserver', class {
        constructor(cb: Callback) {
            callback = cb
        }
        observe = (el: Element) => observed.add(el)
        unobserve = (el: Element) => observed.delete(el)
        disconnect = () => observed.clear()
    })
    intersect = (targets) =>
        callback(targets.filter((t) => observed.has(t)).map((target) => ({ isIntersecting: true, target })))
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
    stopPendingImages()
    vi.unstubAllGlobals()
    vi.useRealTimers()
    document.body.innerHTML = ''
})

function answer(body: { images: Record<string, unknown>; pending: string[] }): void {
    fetchMock.mockResolvedValueOnce({ ok: true, json: async () => body })
}

const slot = (name: string) => document.querySelector(`[data-img-name="${name}"]`)!

describe('pending images', () => {
    it('polls only the slots near the viewport and swaps in the rendered picture', async () => {
        answer({
            images: { '1001.png': { src: '/cdn/blobs/a.png', webp: '/cdn/blobs/a.webp' } },
            pending: ['2003.png'],
        })
        startPendingImages()

        intersect([slot('1001.png'), slot('2003.png')])
        await vi.advanceTimersByTimeAsync(150)

        expect(fetchMock).toHaveBeenCalledTimes(1)
        const url = new URL(String(fetchMock.mock.calls[0][0]), 'http://localhost')
        expect(url.pathname).toBe('/api/images/item')
        expect(url.searchParams.get('version')).toBe('16.16.1')
        expect(url.searchParams.get('names')).toBe('1001.png,2003.png')
        expect(url.searchParams.has('retry')).toBe(false)

        const picture = document.querySelector('#grid img')!
        expect(picture.getAttribute('src')).toBe('/cdn/blobs/a.png')
        expect(picture.getAttribute('alt')).toBe('Boots')
        expect(document.querySelector('#grid source')!.getAttribute('srcset')).toBe('/cdn/blobs/a.webp')
        expect(document.querySelector('[data-img-name="1001.png"]')).toBeNull()
        // Still pending, still a sweep; the hidden one was never asked about.
        expect(slot('2003.png').hasAttribute('data-img-pending')).toBe(true)
        expect(slot('3006.png').hasAttribute('data-img-pending')).toBe(true)
    })

    it('drops the WebP source when the server has no twin and settles absences', async () => {
        answer({ images: { '1001.png': { src: '/cdn/blobs/a.svg', webp: null }, '2003.png': null }, pending: [] })
        startPendingImages()

        intersect([slot('1001.png'), slot('2003.png')])
        await vi.advanceTimersByTimeAsync(150)

        expect(document.querySelector('#grid source')).toBeNull()
        expect(document.querySelector('#grid img')!.getAttribute('src')).toBe('/cdn/blobs/a.svg')
        expect(slot('2003.png').hasAttribute('data-img-pending')).toBe(false)
        expect(slot('2003.png').getAttribute('data-img-state')).toBe('absent')
    })

    it('backs off, flags the last attempt as a retry, then gives up as initials', async () => {
        for (let i = 0; i < 5; i += 1) answer({ images: {}, pending: ['1001.png'] })
        startPendingImages()

        intersect([slot('1001.png')])
        await vi.advanceTimersByTimeAsync(150)
        expect(fetchMock).toHaveBeenCalledTimes(1)

        await vi.advanceTimersByTimeAsync(3000)
        expect(fetchMock).toHaveBeenCalledTimes(2)
        await vi.advanceTimersByTimeAsync(6000)
        expect(fetchMock).toHaveBeenCalledTimes(3)
        await vi.advanceTimersByTimeAsync(12000)
        expect(fetchMock).toHaveBeenCalledTimes(4)
        await vi.advanceTimersByTimeAsync(24000)
        expect(fetchMock).toHaveBeenCalledTimes(5)

        const lastUrl = new URL(String(fetchMock.mock.calls[4][0]), 'http://localhost')
        expect(lastUrl.searchParams.get('retry')).toBe('1')
        expect(slot('1001.png').hasAttribute('data-img-pending')).toBe(false)
        expect(slot('1001.png').getAttribute('data-img-state')).toBe('absent')

        await vi.advanceTimersByTimeAsync(60000)
        expect(fetchMock).toHaveBeenCalledTimes(5)
    })

    it('splits a scope into requests of its batch size', async () => {
        answer({ images: {}, pending: ['1001.png', '2003.png'] })
        answer({ images: {}, pending: ['3006.png'] })
        startPendingImages()

        intersect([slot('1001.png'), slot('2003.png'), slot('3006.png')])
        await vi.advanceTimersByTimeAsync(150)

        expect(fetchMock).toHaveBeenCalledTimes(2)
    })

    it('stops polling when the page is about to be cached or re-rendered', async () => {
        answer({ images: {}, pending: ['1001.png'] })
        startPendingImages()
        intersect([slot('1001.png')])
        await vi.advanceTimersByTimeAsync(150)

        stopPendingImages()
        await vi.advanceTimersByTimeAsync(60000)

        expect(fetchMock).toHaveBeenCalledTimes(1)
    })
})
