import { onBeforeUnmount, onMounted, ref } from 'vue'

/** Detail pages render this marker (server ms + "show the badge here" flag). */
const MARKER = '[data-load-timing]'
const RUNTIME_HEADER = 'X-Runtime'

interface FetchResponseEvent {
    fetchResponse?: { response?: { headers?: { get(name: string): string | null } } }
}

/**
 * Load-time badge state machine, split out of {@link LoadTimeBadge.vue} so the
 * timing/Turbo logic is testable apart from the presentation.
 *
 * The badge is a persistent island (mounted once in base.html.twig). It surfaces
 * only on detail pages, which render a `<… data-load-timing data-server-ms>`
 * marker (see components/detail_actions.html.twig); on any page without it the
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
    const visible = ref(false)
    const serverMs = ref<number | null>(null)
    const clientMs = ref<number | null>(null)

    // Per-navigation scratch state (plain closures, not reactive).
    let navStart: number | null = null
    let headerServerMs: number | null = null

    function onBeforeVisit(): void {
        navStart = performance.now()
    }

    function onFetchResponse(e: Event): void {
        const raw = (e as CustomEvent<FetchResponseEvent>).detail?.fetchResponse?.response?.headers?.get(RUNTIME_HEADER)
        const parsed = raw != null ? Number(raw) : NaN
        if (Number.isFinite(parsed)) {
            headerServerMs = parsed
        }
    }

    /** Client ms of the initial (non-Turbo) load, from the Navigation Timing entry. */
    function navigationMs(): number {
        const nav = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined
        const end = nav ? nav.domContentLoadedEventEnd || nav.responseEnd || 0 : 0

        return nav && end > 0 ? Math.round(end - nav.startTime) : Math.round(performance.now())
    }

    /** Re-evaluate for the page that just became current (initial mount or turbo:load). */
    function activate(): void {
        const marker = document.querySelector<HTMLElement>(MARKER)
        if (!marker) {
            visible.value = false
            navStart = null
            headerServerMs = null

            return
        }

        const inline = Number(marker.dataset.serverMs)
        serverMs.value = headerServerMs ?? (Number.isFinite(inline) ? inline : null)
        clientMs.value = navStart != null ? Math.round(performance.now() - navStart) : navigationMs()
        visible.value = true

        navStart = null
        headerServerMs = null
    }

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

    return { visible, serverMs, clientMs }
}
