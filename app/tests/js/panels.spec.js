import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { loadPanels } from '../../public/admin/js/panels.js'

const SKELETON = '<div class="sk sk-kpi"></div>'

function shell(url = '/admin/panel/storage') {
    document.body.innerHTML = `<div class="panel-async" data-panel-url="${url}">${SKELETON}</div>`

    return document.querySelector('.panel-async')
}

/** Lets the fetch promise chain settle. */
const settle = () => new Promise((resolve) => setTimeout(resolve, 0))

beforeEach(() => {
    document.body.innerHTML = ''
})

afterEach(() => {
    vi.unstubAllGlobals()
})

describe('loadPanels', () => {
    it('swaps the skeleton for the fetched fragment', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => '<section class="card">ok</section>' })
        vi.stubGlobal('fetch', fetchMock)

        const panel = shell()
        loadPanels()
        await settle()

        expect(fetchMock).toHaveBeenCalledOnce()
        expect(fetchMock.mock.calls[0][0]).toBe('/admin/panel/storage')
        expect(panel.dataset.panelState).toBe('ready')
        expect(panel.querySelector('.card')).not.toBeNull()
        expect(panel.querySelector('.sk')).toBeNull()
    })

    it('never fetches the same shell twice', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true, text: async () => '<p>ok</p>' })
        vi.stubGlobal('fetch', fetchMock)

        shell()
        loadPanels()
        loadPanels()
        await settle()

        expect(fetchMock).toHaveBeenCalledOnce()
    })

    it('offers a retry that puts the skeleton back and refetches', async () => {
        const fetchMock = vi.fn()
            .mockResolvedValueOnce({ ok: false, status: 503 })
            .mockResolvedValueOnce({ ok: true, text: async () => '<p>recovered</p>' })
        vi.stubGlobal('fetch', fetchMock)

        const panel = shell()
        loadPanels()
        await settle()

        expect(panel.dataset.panelState).toBe('failed')
        const retry = panel.querySelector('button')
        expect(retry.textContent).toBe('Réessayer')
        expect(panel.textContent).toContain('HTTP 503')

        retry.click()
        await settle()

        expect(panel.dataset.panelState).toBe('ready')
        expect(panel.textContent).toContain('recovered')
    })
})
