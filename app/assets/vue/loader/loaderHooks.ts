import { destinationForSwitch, metaContent } from './urls'
import type { LoaderController } from './LoaderController'
import { WARM_REQUEST_EVENT, type WarmRequestDetail } from './warmBridge'

/** Action of the header version/language switcher form. */
const SWITCH_ACTION = /setup-?submit|setup_save/i

/** Hold a cold resource visit back until its image batch has been warmed. */
function beforeVisitHandler(loader: LoaderController): (e: Event) => void {
    return (e) => {
        const url = (e as CustomEvent<{ url?: string }>).detail?.url
        if (url && loader.gateVisit(url)) e.preventDefault()
    }
}

/**
 * In-place warm request from another island (build editor switching to a cold
 * patch). Same SSE stream as navigation, but on completion the loader resumes
 * the caller (its picker reload lands on warm images) instead of visiting.
 * Claiming it via preventDefault is what makes {@link requestWarm} await us
 * rather than resolve itself.
 */
function warmRequestHandler(loader: LoaderController): (e: Event) => void {
    return (e) => {
        const request = (e as CustomEvent<WarmRequestDetail>).detail
        if (!request?.version || !request?.lang || !request?.path
            || typeof request.resolve !== 'function') {
            return
        }
        e.preventDefault()
        loader.warmInPlace(request)
    }
}

/**
 * Version/language switcher (header, on every page) posts to /setup-submit then
 * reloads the current page under the new selection. A switch always triggers a
 * cold backend reload, so gate it from ANY page — not just the list/home routes.
 */
function switchSubmitHandler(loader: LoaderController): (e: Event) => void {
    return (e) => {
        const form = e.target
        if (!(form instanceof HTMLFormElement)) return
        if (!SWITCH_ACTION.test(form.getAttribute('action') ?? '')) return

        const formData = new FormData(form)
        const version = String(formData.get('version') ?? '')
        const lang = String(formData.get('langue') ?? '')
        if (!version || !lang) return

        e.preventDefault()
        // The 302 is not followed — `dest` already carries the switched selection
        // (path segment or query).
        const dest = destinationForSwitch(version, lang, metaContent('dd-latest'))
        loader.prepareSwitch(dest)
        void persistSelection(form, formData)
            .finally(() => loader.gateSwitch(dest, { version, lang }))
    }
}

/** Best effort: on failure the dest path/query still drives the selection. */
function persistSelection(form: HTMLFormElement, formData: FormData): Promise<unknown> {
    return fetch(form.action, {
        method: 'POST',
        body: formData,
        redirect: 'manual',
        credentials: 'same-origin',
    }).catch(() => undefined)
}

/**
 * Wire the loader to Turbo (before-visit / load / switcher submit) and to the
 * cross-island warm bridge. Returns the matching unbind function.
 */
export function bindLoaderHooks(loader: LoaderController): () => void {
    const onBeforeVisit = beforeVisitHandler(loader)
    const onWarmRequest = warmRequestHandler(loader)
    const onSubmit = switchSubmitHandler(loader)
    const onLoad = (): void => loader.dismiss()

    document.addEventListener('turbo:before-visit', onBeforeVisit)
    document.addEventListener('turbo:load', onLoad)
    document.addEventListener('submit', onSubmit, true)
    document.addEventListener(WARM_REQUEST_EVENT, onWarmRequest)

    return () => {
        document.removeEventListener('turbo:before-visit', onBeforeVisit)
        document.removeEventListener('turbo:load', onLoad)
        document.removeEventListener('submit', onSubmit, true)
        document.removeEventListener(WARM_REQUEST_EVENT, onWarmRequest)
    }
}
