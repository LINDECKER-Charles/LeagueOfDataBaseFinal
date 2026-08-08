import { onBeforeUnmount, onMounted, ref, type Ref } from 'vue'

/** Detail pages render this marker (server ms + "show the badge here" flag). */
const MARKER = '[data-load-timing]'
const RUNTIME_HEADER = 'X-Runtime'

interface FetchResponseEvent {
    fetchResponse?: { response?: { headers?: { get(name: string): string | null } } }
}

/** Published state + the per-navigation scratch the two sources land in. */
interface TimingState {
    visible: Ref<boolean>
    serverMs: Ref<number | null>
    clientMs: Ref<number | null>
    navStart: number | null
    headerServerMs: number | null
}

/**
 * Load-time badge state machine, split out of {@link LoadTimeBadge.vue} so the
 * timing/Turbo logic is testable apart from the presentation.
 *
 * The badge is a persistent island (mounted once in base.html.twig). It surfaces
 * only on detail pages, which render a `<… data-load-timing data-server-ms>`
 * marker (see components/codex/detail_actions.html.twig); on any page without it the
 * badge self-hides.
 *
 * Client "perceived" time:
 *  - Turbo soft navigation: measured from `turbo:before-visit` → `turbo:load`.
 *  - Initial hard load: from the Navigation Timing entry (time to HTML received).
 *
 * Server time: the `X-Runtime` header captured off the Turbo fetch response when
 * present (the canonical figure), else the marker's inline `data-server-ms` — the
 * only source reachable on the initial load, where the response header is not.
 */
export function useLoadTiming() {
    const state: TimingState = {
        visible: ref(false),
        serverMs: ref<number | null>(null),
        clientMs: ref<number | null>(null),
        navStart: null,
        headerServerMs: null,
    }

    const onBeforeVisit = (): void => void (state.navStart = performance.now())
    const onFetchResponse = (e: Event): void => captureRuntimeHeader(state, e)
    const activate = (): void => activatePage(state)

    onMounted(() => {
        document.addEventListener('turbo:before-visit', onBeforeVisit)
        document.addEventListener('turbo:before-fetch-response', onFetchResponse)
        document.addEventListener('turbo:load', activate)
        activate() // initial (hard) load — turbo:load also fires, activate() is idempotent
    })
    onBeforeUnmount(() => {
        document.removeEventListener('turbo:before-visit', onBeforeVisit)
        document.removeEventListener('turbo:before-fetch-response', onFetchResponse)
        document.removeEventListener('turbo:load', activate)
    })

    return { visible: state.visible, serverMs: state.serverMs, clientMs: state.clientMs }
}

function captureRuntimeHeader(state: TimingState, e: Event): void {
    const raw = (e as CustomEvent<FetchResponseEvent>).detail
        ?.fetchResponse?.response?.headers?.get(RUNTIME_HEADER)
    const parsed = raw != null ? Number(raw) : NaN
    if (Number.isFinite(parsed)) {
        state.headerServerMs = parsed
    }
}

/** Client ms of the initial (non-Turbo) load, from the Navigation Timing entry. */
function navigationMs(): number {
    const nav = performance.getEntriesByType('navigation')[0] as
        | PerformanceNavigationTiming
        | undefined
    const end = nav ? nav.domContentLoadedEventEnd || nav.responseEnd || 0 : 0

    return nav && end > 0 ? Math.round(end - nav.startTime) : Math.round(performance.now())
}

/** Re-evaluate for the page that just became current (initial mount or turbo:load). */
function activatePage(state: TimingState): void {
    const marker = document.querySelector<HTMLElement>(MARKER)
    if (!marker) {
        state.visible.value = false
        resetScratch(state)

        return
    }

    const inline = Number(marker.dataset.serverMs)
    state.serverMs.value = state.headerServerMs ?? (Number.isFinite(inline) ? inline : null)
    state.clientMs.value = state.navStart != null
        ? Math.round(performance.now() - state.navStart)
        : navigationMs()
    state.visible.value = true

    resetScratch(state)
}

function resetScratch(state: TimingState): void {
    state.navStart = null
    state.headerServerMs = null
}
