/**
 * Test-only doubles and event helpers for the loader island. Lives next to the
 * state machine it drives so the spec file stays about the behaviours asserted,
 * not about the plumbing needed to trigger them. Never imported by the bundle.
 */

/** Props the Twig host renders the island with. */
export const loaderProps = {
    eyebrow: 'Data Dragon',
    title: 'Summoning data',
    subtitle: 'Fetching…',
    preparing: 'Preparing…',
    status: { fetching: 'fetching', ready: 'ready' },
    labels: {
        champions: 'Champions',
        items: 'Items',
        runes: 'Runes',
        summoners: 'Summoner Spells',
    },
}

/** In-memory EventSource the component opens against /api/loader/prepare. */
export class FakeEventSource {
    static instances = 0
    static last: FakeEventSource | null = null
    url: string
    closed = false
    onerror: (() => void) | null = null
    private listeners: Record<string, ((e: { data: string }) => void)[]> = {}

    constructor(url: string) {
        this.url = url
        FakeEventSource.instances++
        FakeEventSource.last = this
    }

    addEventListener(type: string, cb: (e: { data: string }) => void): void {
        ;(this.listeners[type] ??= []).push(cb)
    }

    emit(type: string, data: unknown): void {
        ;(this.listeners[type] ?? []).forEach((cb) => cb({ data: JSON.stringify(data) }))
    }

    fail(): void {
        this.onerror?.()
    }

    close(): void {
        this.closed = true
    }
}

/**
 * Dispatch a cancelable before-visit and hand the event back so specs can read
 * defaultPrevented.
 */
export function beforeVisit(url: string): CustomEvent {
    const e = new CustomEvent('turbo:before-visit', { detail: { url }, cancelable: true })
    document.dispatchEvent(e)

    return e
}

export const turboLoad = (): boolean => document.dispatchEvent(new CustomEvent('turbo:load'))

/** Dispatch the header version/language switcher submit the controller listens for. */
export function switchSubmit(version: string, lang: string, action = '/setup-submit'): void {
    const form = document.createElement('form')
    form.setAttribute('action', action)
    const v = document.createElement('input'); v.name = 'version'; v.value = version
    const l = document.createElement('input'); l.name = 'langue'; l.value = lang
    form.append(v, l)
    document.body.appendChild(form)
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    form.remove()
}
