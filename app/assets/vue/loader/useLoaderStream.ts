import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import {
    destinationForSwitch,
    meta,
    parse,
    prepareUrl,
    resolveVL,
    resourcesFor,
    TYPE_TO_KEY,
    turbo,
    warmKey,
    type Phase,
    type ResourceKey,
} from './urls'
import { WARM_REQUEST_EVENT, type WarmRequestDetail } from './warmBridge'

const READY_HOLD = 450 // once shown, hold 100%/"ready" long enough to read
const WATCHDOG_IDLE = 15000 // no stream activity for this long → assume it's dead and visit anyway
const LOG_LIMIT = 6 // entries retained in the live buffer (only the latest is surfaced)

/**
 * SSE "gate-then-visit" state machine for the navigation loader, extracted from
 * {@link ResourceLoader.vue} so the orchestration is testable and readable apart
 * from the presentation. Owns the reactive state, the EventSource lifecycle + the
 * inactivity watchdog, and the Turbo hooks (before-visit / load / submit). Returns
 * only the reactive surface the template (and the test seam) bind to.
 */
export function useLoaderStream() {
    const visible = ref(false)
    const finishing = ref(false)
    const phase = ref<Phase>('idle')
    const progress = ref(0)
    const active = ref<ResourceKey[]>([])
    const readyKeys = ref<ResourceKey[]>([])
    const entries = ref<{ id: number; name: string; key: ResourceKey }[]>([])

    const current = computed(() => entries.value.at(-1) ?? null)
    const pct = computed(() => Math.min(100, Math.round(progress.value * 100)))

    // Per-run mutable state (not reactive — plain closures).
    let es: EventSource | null = null
    let generation = 0
    let entrySeq = 0
    let holdTimer: ReturnType<typeof setTimeout> | undefined
    let watchdog: ReturnType<typeof setTimeout> | undefined
    let bypassUrl: string | null = null
    // Set for an in-place warm (build editor): run to completion, then hand back
    // to the caller instead of performing a Turbo visit.
    let onComplete: (() => void) | null = null
    let finished = false
    const warmed = new Set<string>()
    const catTotal = new Map<ResourceKey, number>()
    const catCount = new Map<ResourceKey, number>()

    function clearTimers(): void {
        if (holdTimer) { clearTimeout(holdTimer); holdTimer = undefined }
        if (watchdog) { clearTimeout(watchdog); watchdog = undefined }
    }

    function closeStream(): void {
        if (es) { es.close(); es = null }
    }

    function resetRun(path: string, activeKeys?: ResourceKey[]): void {
        finished = false
        progress.value = 0
        phase.value = 'preparing'
        finishing.value = false
        // A warm-in-place run names its own resources (the nav routes never render
        // this token); everything else derives them from the destination path.
        active.value = activeKeys ?? resourcesFor(path)
        readyKeys.value = []
        entries.value = []
        catTotal.clear()
        catCount.clear()
    }

    /** Reset the overlay to hidden/idle (in-place warm end, or once a warm visit lands). */
    function hideOverlay(): void {
        visible.value = false
        finishing.value = false
        phase.value = 'idle'
    }

    /** Perform the gated visit; the re-fired before-visit is recognised by URL and let through. */
    function navigateWarm(url: string): void {
        bypassUrl = url
        const t = turbo()
        if (t?.visit) {
            t.visit(url)
        } else {
            window.location.assign(url)
        }
        bypassUrl = null
    }

    /** End a run with no image work: an in-place warm hands back, a navigation visits. */
    function finishNoWork(destUrl: string): void {
        const cb = onComplete
        onComplete = null
        if (cb) { hideOverlay(); cb(); return }
        navigateWarm(destUrl)
    }

    function startPrepare(
        destUrl: string,
        override?: { version: string; lang: string },
        opts?: { eager?: boolean; activeKeys?: ResourceKey[]; onComplete?: () => void },
    ): void {
        const { version, lang } = resolveVL(destUrl, override)
        // A new run supersedes any still-pending in-place warm; resolve its callback
        // so the awaiting caller (build editor) is never left hanging.
        const superseded = onComplete
        onComplete = opts?.onComplete ?? null
        if (superseded && superseded !== onComplete) superseded()
        if (!version || !lang) { finishNoWork(destUrl); return }

        const gen = ++generation
        closeStream()
        clearTimers()

        const path = new URL(destUrl, window.location.origin).pathname
        resetRun(path, opts?.activeKeys)

        // A deliberate version/language switch (`eager`) always triggers a cold
        // reload, so raise the overlay up-front instead of waiting for `start`. A
        // batch-less destination (detail, profile…) has nothing to stream, so skip
        // the SSE and let the overlay cover the cold server render until the
        // destination's turbo:load hides it. Plain navigations keep the honest
        // "surface only on real image work (total > 0)" rule below.
        if (opts?.eager) {
            visible.value = true
            if (active.value.length === 0) { finishNoWork(destUrl); return }
        }

        // The overlay is NOT shown on a timer: it surfaces only once `start` reports
        // real image work to do (total > 0). A warm destination therefore never
        // flashes it, whatever the SSE round-trip latency (dev boot, slow network).

        // Inactivity watchdog: a cold page legitimately outlasts any fixed budget, so
        // we don't cap the total — we only bail if the stream falls silent (no start/
        // phase/item) for WATCHDOG_IDLE. Every event below re-arms it, so a long warm
        // that keeps progressing is never cut, while a truly dead stream still is.
        const rearmWatchdog = (): void => {
            if (watchdog) clearTimeout(watchdog)
            watchdog = setTimeout(() => { if (gen === generation) finishRun(destUrl, version, lang, gen) }, WATCHDOG_IDLE)
        }
        rearmWatchdog()

        try {
            es = new EventSource(prepareUrl(destUrl, version, lang))
        } catch {
            finishRun(destUrl, version, lang, gen)
            return
        }

        es.addEventListener('start', (ev) => {
            if (gen !== generation) return
            rearmWatchdog()
            const d = parse(ev)
            const cats = (d.categories ?? {}) as Record<string, number>
            for (const [type, n] of Object.entries(cats)) {
                const key = TYPE_TO_KEY[type]
                if (!key) continue
                catTotal.set(key, n)
                catCount.set(key, 0)
                if (n === 0) markReady(key)
            }
            phase.value = 'loading'
            // Surface the overlay only when there is genuine Riot image warming to
            // show; a warm destination (total 0) resolves straight to the visit.
            if (d.total) visible.value = true
            else progress.value = 1
        })

        // Dataset phase (cold JSON fetch) emits `phase` before `start`; keep the
        // watchdog alive across it so a slow-but-progressing warm isn't cut.
        es.addEventListener('phase', () => { if (gen === generation) rearmWatchdog() })

        es.addEventListener('item', (ev) => {
            if (gen !== generation) return
            rearmWatchdog()
            const d = parse(ev)
            const key = TYPE_TO_KEY[String(d.category)] ?? active.value[0]
            entries.value.push({ id: ++entrySeq, name: String(d.name ?? ''), key })
            if (entries.value.length > LOG_LIMIT * 3) entries.value = entries.value.slice(-LOG_LIMIT * 2)
            if (d.total) progress.value = Math.min(1, Number(d.index) / Number(d.total))
            if (key) {
                catCount.set(key, (catCount.get(key) ?? 0) + 1)
                if ((catCount.get(key) ?? 0) >= (catTotal.get(key) ?? 0)) markReady(key)
            }
        })

        es.addEventListener('done', () => finishRun(destUrl, version, lang, gen))

        es.onerror = () => {
            // Opaque + auto-reconnecting: on a real failure (not our own close after
            // done) bail out to a normal visit so navigation never gets stuck.
            if (finished || gen !== generation) return
            finishRun(destUrl, version, lang, gen)
        }
    }

    function markReady(key: ResourceKey): void {
        if (!readyKeys.value.includes(key)) readyKeys.value = [...readyKeys.value, key]
    }

    function finishRun(destUrl: string, version: string, lang: string, gen: number): void {
        if (finished || gen !== generation) return
        finished = true
        closeStream()
        if (watchdog) { clearTimeout(watchdog); watchdog = undefined }

        progress.value = 1
        phase.value = 'done'
        finishing.value = true
        active.value.forEach(markReady)
        warmed.add(warmKey(destUrl, version, lang))

        const cb = onComplete
        onComplete = null

        // In-place warm (build editor): resume the caller instead of visiting. If
        // the overlay surfaced, hold the "ready" beat first; a warm no-op (never
        // shown) resumes immediately. Reload is kicked BEFORE hiding so the pickers
        // swap to their loading state under the overlay, never flashing stale rows.
        if (cb) {
            if (visible.value) {
                holdTimer = setTimeout(() => { holdTimer = undefined; cb(); hideOverlay() }, READY_HOLD)
            } else {
                hideOverlay()
                cb()
            }
            return
        }

        if (!visible.value) {
            navigateWarm(destUrl)
        } else {
            holdTimer = setTimeout(() => { holdTimer = undefined; navigateWarm(destUrl) }, READY_HOLD)
        }
    }

    function onBeforeVisit(e: Event): void {
        const url = (e as CustomEvent<{ url?: string }>).detail?.url
        if (!url) return
        if (url === bypassUrl) { bypassUrl = null; return } // our gated visit — let it through
        const path = new URL(url, window.location.origin).pathname
        if (!resourcesFor(path).length) return
        const { version, lang } = resolveVL(url)
        if (!version || !lang) return // no known selection → normal visit (deferral covers it)
        if (warmed.has(warmKey(url, version, lang))) return // already warmed this session
        e.preventDefault()
        startPrepare(url, { version, lang })
    }

    /** Hide once the (warm) destination has rendered — honest: overlay leaves when the page is ready. */
    function onLoad(): void {
        if (!visible.value && phase.value === 'idle') return
        clearTimers()
        closeStream()
        hideOverlay()
    }

    /**
     * In-place warm request from another island (build editor switching to a cold
     * patch). Same SSE stream as navigation, but on completion we resume the caller
     * (its picker reload lands on warm images) instead of visiting. Claim it via
     * preventDefault so {@link requestWarm} awaits our `done()` rather than resolving
     * itself; an unknown version/lang or a fully-warm patch simply never surfaces.
     */
    function onWarmRequest(e: Event): void {
        const d = (e as CustomEvent<WarmRequestDetail>).detail
        if (!d?.version || !d?.lang || !d?.path || typeof d.resolve !== 'function') return
        e.preventDefault()
        startPrepare(
            d.path,
            { version: d.version, lang: d.lang },
            { activeKeys: resourcesFor(d.path), onComplete: d.resolve },
        )
    }

    /**
     * Version/language switcher (header, on every page) posts to /setup-submit then
     * reloads the current page under the new selection. A switch always triggers a
     * cold backend reload, so gate it from ANY page — not just the list/home routes
     * — raising the loader the instant we start fetching.
     */
    function onSubmit(e: Event): void {
        const form = e.target
        if (!(form instanceof HTMLFormElement)) return
        const action = form.getAttribute('action') ?? ''
        if (!/setup-?submit|setup_save/i.test(action)) return

        const fd = new FormData(form)
        const version = String(fd.get('version') ?? '')
        const lang = String(fd.get('langue') ?? '')
        if (!version || !lang) return

        const dest = destinationForSwitch(version, lang, meta('dd-latest'))
        e.preventDefault()

        // Raise the overlay before the prefs round-trip so feedback is instant;
        // startPrepare(eager) then streams a batch destination or covers a
        // batch-less one. The 302 is not followed — `dest` already carries the
        // switched selection (path segment or query).
        closeStream()
        clearTimers()
        resetRun(new URL(dest, window.location.origin).pathname)
        visible.value = true
        fetch(form.action, { method: 'POST', body: fd, redirect: 'manual', credentials: 'same-origin' })
            .catch(() => { /* best effort: dest path/query still drives the selection */ })
            .finally(() => startPrepare(dest, { version, lang }, { eager: true }))
    }

    onMounted(() => {
        document.addEventListener('turbo:before-visit', onBeforeVisit)
        document.addEventListener('turbo:load', onLoad)
        document.addEventListener('submit', onSubmit, true)
        document.addEventListener(WARM_REQUEST_EVENT, onWarmRequest)
    })
    onBeforeUnmount(() => {
        document.removeEventListener('turbo:before-visit', onBeforeVisit)
        document.removeEventListener('turbo:load', onLoad)
        document.removeEventListener('submit', onSubmit, true)
        document.removeEventListener(WARM_REQUEST_EVENT, onWarmRequest)
        clearTimers()
        closeStream()
    })

    return { visible, finishing, phase, progress, active, readyKeys, entries, current, pct }
}
