import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { setupProfileForm } from './profileForm'

/**
 * The favorites form enhancement: auto-saves on `profile:changed` and on the
 * visibility switch, and live-reflects the visibility state — without ever
 * touching the no-JS submit contract beyond hiding the button.
 */
function buildForm(): HTMLFormElement {
    document.body.innerHTML = `
        <form action="/profile" data-autosave>
            <input type="hidden" name="_token" value="tok">
            <div data-visibility class="visibility-card visibility-card--private"
                 data-state-public="Public" data-state-private="Private">
                <span data-visibility-state>Private</span>
                <div data-visibility-public-only hidden></div>
                <input type="checkbox" name="isPublicProfile" value="1">
            </div>
            <input type="hidden" name="favoriteSkinId" value="Ahri_7">
            <span data-autosave-status hidden
                  data-label-saving="Saving" data-label-saved="Saved"
                  data-label-error="Err" data-label-dropped="Dropped"></span>
            <button type="submit" data-autosave-hide>Save</button>
        </form>`
    return document.querySelector('form') as HTMLFormElement
}

function mockFetch(body: Record<string, unknown>, ok = true): ReturnType<typeof vi.fn> {
    const fetchMock = vi.fn(async () => ({ ok, json: async () => body }) as Response)
    vi.stubGlobal('fetch', fetchMock)
    return fetchMock
}

describe('setupProfileForm', () => {
    beforeEach(() => vi.useFakeTimers())
    afterEach(() => {
        vi.useRealTimers()
        vi.restoreAllMocks()
        document.body.innerHTML = ''
    })

    it('hides the manual submit and reveals the status region on bind', () => {
        const form = buildForm()
        setupProfileForm()

        expect(form.querySelector<HTMLElement>('[data-autosave-hide]')!.hidden).toBe(true)
        expect(form.querySelector<HTMLElement>('[data-autosave-status]')!.hidden).toBe(false)
    })

    it('debounces a POST save on profile:changed and marks it saved', async () => {
        const form = buildForm()
        const fetchMock = mockFetch({ ok: true })
        setupProfileForm()

        form.dispatchEvent(new CustomEvent('profile:changed', { bubbles: true }))
        expect(fetchMock).not.toHaveBeenCalled() // still within the debounce window

        await vi.advanceTimersByTimeAsync(600)

        expect(fetchMock).toHaveBeenCalledOnce()
        const [url, init] = fetchMock.mock.calls[0]!
        expect(String(url)).toMatch(/\/profile$/) // form.action resolves to an absolute URL in jsdom
        expect((init as RequestInit).method).toBe('POST')
        expect((init as RequestInit).body).toBeInstanceOf(FormData)
        expect(form.querySelector('[data-autosave-status]')!.textContent).toBe('Saved')
    })

    it('live-syncs the visibility card and saves when the switch flips', async () => {
        const form = buildForm()
        mockFetch({ ok: true, isPublicProfile: true })
        setupProfileForm()

        const toggle = form.querySelector<HTMLInputElement>('input[name="isPublicProfile"]')!
        toggle.checked = true
        toggle.dispatchEvent(new Event('change', { bubbles: true }))

        const card = form.querySelector<HTMLElement>('[data-visibility]')!
        expect(card.classList.contains('visibility-card--public')).toBe(true)
        expect(card.querySelector('[data-visibility-state]')!.textContent).toBe('Public')
        expect(form.querySelector<HTMLElement>('[data-visibility-public-only]')!.hidden).toBe(false)

        await vi.advanceTimersByTimeAsync(600)
        expect(fetch).toHaveBeenCalledOnce()
    })

    it('surfaces a dropped-favorite warning from the response', async () => {
        const form = buildForm()
        mockFetch({ ok: true, invalidFavorites: ['champion'] })
        setupProfileForm()

        form.dispatchEvent(new CustomEvent('profile:changed', { bubbles: true }))
        await vi.advanceTimersByTimeAsync(600)

        const status = form.querySelector('[data-autosave-status]')!
        expect(status.textContent).toBe('Dropped')
        expect(status.classList.contains('is-warned')).toBe(true)
    })
})
