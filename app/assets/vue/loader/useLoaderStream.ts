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

    function resetRun(path: string): void {
        finished = false
        progress.value = 0
        phase.value = 'preparing'
        finishing.value = false
        active.value = resourcesFor(path)
        readyKeys.value = []
        entries.value = []
        catTotal.clear()
        catCount.clear()
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

    function startPrepare(destUrl: string, override?: { version: string; lang: string }): void {
        const { version, lang } = resolveVL(destUrl, override)
        if (!version || !lang) { navigateWarm(destUrl); return }

        const gen = ++generation
        closeStream()
        clearTimers()

        const path = new URL(destUrl, window.location.origin).pathname
        resetRun(path)

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
        visible.value = false
        finishing.value = false
        phase.value = 'idle'
    }

    /** Version/language switcher posts to /setup-submit then reloads the current page — gate that too. */
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
        if (!resourcesFor(new URL(dest, window.location.origin).pathname).length) return

        e.preventDefault()
        // Persist prefs (session + remember cookie) without following the redirect,
        // then warm the destination and visit it.
        fetch(form.action, { method: 'POST', body: fd, redirect: 'manual', credentials: 'same-origin' })
            .catch(() => { /* best effort: query params still drive the switched list */ })
            .finally(() => startPrepare(dest, { version, lang }))
    }

    onMounted(() => {
        document.addEventListener('turbo:before-visit', onBeforeVisit)
        document.addEventListener('turbo:load', onLoad)
        document.addEventListener('submit', onSubmit, true)
    })
    onBeforeUnmount(() => {
        document.removeEventListener('turbo:before-visit', onBeforeVisit)
        document.removeEventListener('turbo:load', onLoad)
        document.removeEventListener('submit', onSubmit, true)
        clearTimers()
        closeStream()
    })

    return { visible, finishing, phase, progress, active, readyKeys, entries, current, pct }
}
