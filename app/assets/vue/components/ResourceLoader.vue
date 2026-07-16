<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

/**
 * Global navigation loader island — persists across Turbo visits via
 * `data-turbo-permanent`.
 *
 * "Gate-then-visit": for a cold, un-warmed destination we intercept the Turbo
 * visit, stream the real DDragon ingestion from `GET /api/loader/prepare`
 * (Server-Sent Events), drive a determinate bar + name each resource as it
 * lands, and only THEN perform the (now warm) visit. Warm destinations resolve
 * instantly (total 0) and never flash the overlay.
 *
 * Honesty note: the bar reflects images actually stored into object storage
 * (`index/total` from the stream), and "ready" fires on the real `done` event —
 * no fabricated percentage.
 */
interface Labels {
    champions: string
    items: string
    runes: string
    summoners: string
}

defineProps<{
    eyebrow?: string
    title?: string
    subtitle?: string
    preparing?: string
    labels: Labels
    status: { fetching: string; ready: string }
}>()

type ResourceKey = keyof Labels
type Phase = 'idle' | 'preparing' | 'loading' | 'done'

/** Server resource type ('runesReforged'…) → the label/icon key used here. */
const TYPE_TO_KEY: Record<string, ResourceKey> = {
    champion: 'champions',
    item: 'items',
    summoner: 'summoners',
    runesReforged: 'runes',
}

/** Only the home + list routes ingest an image batch worth streaming. */
function resourcesFor(pathname: string): ResourceKey[] {
    const p = (pathname.replace(/\/+$/, '') || '/').toLowerCase()
    if (p === '/home') return ['champions', 'items', 'runes', 'summoners']
    if (p === '/champions') return ['champions']
    if (p === '/objects') return ['items']
    if (p === '/runes') return ['runes']
    if (p === '/summoners') return ['summoners']
    return []
}

const SHOW_DELAY = 280 // warm visits resolve first and never flash the overlay
const READY_HOLD = 450 // once shown, hold 100%/"ready" long enough to read
const WATCHDOG_IDLE = 15000 // no stream activity for this long → assume it's dead and visit anyway
const LOG_LIMIT = 6 // live rows kept on screen

const visible = ref(false)
const finishing = ref(false)
const phase = ref<Phase>('idle')
const progress = ref(0)
const active = ref<ResourceKey[]>([])
const readyKeys = ref<ResourceKey[]>([])
const entries = ref<{ id: number; name: string; key: ResourceKey }[]>([])

const recentEntries = computed(() => entries.value.slice(-LOG_LIMIT))
const pct = computed(() => Math.min(100, Math.round(progress.value * 100)))

// Per-run mutable state (not reactive — plain module-scoped closures).
let es: EventSource | null = null
let generation = 0
let entrySeq = 0
let showTimer: ReturnType<typeof setTimeout> | undefined
let holdTimer: ReturnType<typeof setTimeout> | undefined
let watchdog: ReturnType<typeof setTimeout> | undefined
let bypassUrl: string | null = null
let finished = false
const warmed = new Set<string>()
const catTotal = new Map<ResourceKey, number>()
const catCount = new Map<ResourceKey, number>()

function meta(name: string): string {
    return document.querySelector(`meta[name="${name}"]`)?.getAttribute('content') ?? ''
}

function resolveVL(destUrl: string, override?: { version: string; lang: string }): { version: string; lang: string } {
    if (override) return override
    const u = new URL(destUrl, window.location.origin)
    return {
        version: u.searchParams.get('version') || meta('dd-version'),
        lang: u.searchParams.get('lang') || meta('dd-lang'),
    }
}

function warmKey(destUrl: string, version: string, lang: string): string {
    const u = new URL(destUrl, window.location.origin)
    return [version, lang, u.pathname, u.searchParams.get('numpage') ?? '', u.searchParams.get('itemperpage') ?? ''].join('|')
}

function prepareUrl(destUrl: string, version: string, lang: string): string {
    const u = new URL(destUrl, window.location.origin)
    const q = new URLSearchParams({ path: u.pathname, version, lang })
    const np = u.searchParams.get('numpage')
    const ip = u.searchParams.get('itemperpage')
    if (np) q.set('numpage', np)
    if (ip) q.set('itemperpage', ip)
    return `/api/loader/prepare?${q.toString()}`
}

function clearTimers(): void {
    if (showTimer) { clearTimeout(showTimer); showTimer = undefined }
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

function turbo(): { visit?: (url: string) => void } | undefined {
    return (window as unknown as { Turbo?: { visit?: (url: string) => void } }).Turbo
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

    showTimer = setTimeout(() => {
        showTimer = undefined
        if (gen === generation && !finished) visible.value = true
    }, SHOW_DELAY)

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
        if (!d.total) progress.value = 1
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
        if (showTimer) { clearTimeout(showTimer); showTimer = undefined }
        navigateWarm(destUrl)
    } else {
        holdTimer = setTimeout(() => { holdTimer = undefined; navigateWarm(destUrl) }, READY_HOLD)
    }
}

function parse(ev: Event): Record<string, unknown> {
    try {
        return JSON.parse((ev as MessageEvent).data) as Record<string, unknown>
    } catch {
        return {}
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

    const dest = destinationForSwitch(version, lang)
    if (!resourcesFor(new URL(dest, window.location.origin).pathname).length) return

    e.preventDefault()
    // Persist prefs (session + remember cookie) without following the redirect,
    // then warm the destination and visit it.
    fetch(form.action, { method: 'POST', body: fd, redirect: 'manual', credentials: 'same-origin' })
        .catch(() => { /* best effort: query params still drive the switched list */ })
        .finally(() => startPrepare(dest, { version, lang }))
}

function destinationForSwitch(version: string, lang: string): string {
    const u = new URL(window.location.href)
    u.searchParams.set('version', version)
    u.searchParams.set('lang', lang)
    return `${u.pathname}?${u.searchParams.toString()}`
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

// Test seam: expose the state machine so specs assert on it directly.
defineExpose({ visible, finishing, active, phase, progress, entries })
</script>

<template>
    <div
        class="hx-loader"
        :class="{ 'is-open': visible }"
        role="status"
        aria-live="polite"
        :aria-busy="visible && !finishing"
        :aria-hidden="!visible"
    >
        <div class="hx-loader__panel hextech-frame">
            <!-- Signature: charging Hextech core -->
            <div class="hx-core" aria-hidden="true">
                <span class="hx-core__glow"></span>
                <svg viewBox="0 0 120 120" class="hx-core__svg">
                    <polygon class="hx-core__ring" points="112,60 86,105 34,105 8,60 34,15 86,15" fill="none" />
                    <polygon class="hx-core__sweep" points="112,60 86,105 34,105 8,60 34,15 86,15" fill="none" pathLength="100" />
                    <polygon class="hx-core__inner" points="98,60 79,93 41,93 22,60 41,27 79,27" />
                    <g class="hx-core__orbit">
                        <circle cx="60" cy="8" r="2.6" />
                        <circle cx="105" cy="86" r="2.6" />
                        <circle cx="15" cy="86" r="2.6" />
                    </g>
                    <rect class="hx-core__gem" x="50" y="50" width="20" height="20" rx="1.5" />
                </svg>
            </div>

            <p class="hx-loader__eyebrow eyebrow">{{ eyebrow }}</p>
            <h2 class="hx-loader__title">{{ title }}</h2>
            <p class="hx-loader__subtitle">{{ phase === 'preparing' ? (preparing ?? subtitle) : subtitle }}</p>

            <!-- The manifest: resources the destination page is loading -->
            <ul class="hx-manifest">
                <li
                    v-for="(key, i) in active"
                    :key="key"
                    class="hx-row"
                    :class="readyKeys.includes(key) ? 'hx-row--ready' : 'hx-row--fetching'"
                    :style="{ '--i': i }"
                >
                    <span class="hx-row__icon">
                        <svg v-if="key === 'champions'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M14.5 4 20 4l0 5.5" /><path d="M20 4 4 20" /><path d="M9.5 20 4 20l0-5.5" /><path d="M4 4l5 5" /><path d="M20 20l-5-5" />
                        </svg>
                        <svg v-else-if="key === 'items'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M12 2 21 7v10l-9 5-9-5V7z" /><path d="M12 12 21 7" /><path d="M12 12v10" /><path d="M12 12 3 7" />
                        </svg>
                        <svg v-else-if="key === 'runes'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
                            <circle cx="12" cy="12" r="8.5" /><path d="M12 3.5v17" /><path d="M12 12 18 8" /><path d="M12 12 6 8" />
                        </svg>
                        <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M12 3c1.5 3-1 4.5 0 7 2.5-1 3-3.5 2-5.5C17.5 6 19 9 19 12a7 7 0 1 1-13.4-2.8C6.4 11 7.8 11.6 9 11c-1.2-2.4.3-6.5 3-8z" />
                        </svg>
                    </span>
                    <span class="hx-row__label">{{ labels[key] }}</span>
                    <span class="hx-row__lead" aria-hidden="true"></span>
                    <span class="hx-row__status">
                        <svg v-if="readyKeys.includes(key)" class="hx-row__check" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                            <path d="M3 8.5 6.5 12 13 4.5" />
                        </svg>
                        {{ readyKeys.includes(key) ? status.ready : status.fetching }}
                    </span>
                </li>
            </ul>

            <!-- Live manifest: names streamed from the server as each image lands.
                 Visual only — aria-hidden so screen readers aren't flooded. -->
            <ul class="hx-log" aria-hidden="true">
                <li v-for="entry in recentEntries" :key="entry.id" class="hx-log__row">
                    <span class="hx-log__dot" :class="'hx-log__dot--' + entry.key"></span>
                    <span class="hx-log__name">{{ entry.name }}</span>
                </li>
            </ul>

            <!-- Determinate progress bar -->
            <div class="hx-bar" :class="{ 'hx-bar--indeterminate': phase === 'preparing' }">
                <span class="hx-bar__fill" :style="{ width: pct + '%' }"></span>
            </div>
            <p class="hx-bar__pct" aria-hidden="true">{{ pct }}%</p>
        </div>
    </div>
</template>

<style scoped>
.hx-loader {
    position: fixed;
    inset: 0;
    z-index: 120;
    display: grid;
    place-items: center;
    padding: 1.5rem;
    background: radial-gradient(130% 120% at 50% 28%, rgba(10, 20, 40, 0.72), rgba(1, 10, 19, 0.94));
    backdrop-filter: blur(6px);
    -webkit-backdrop-filter: blur(6px);
    /* Visibility is a deterministic class toggle (no <Transition> leave to await). */
    opacity: 0;
    visibility: hidden;
    pointer-events: none;
    transition: opacity 0.3s var(--ease-hextech), visibility 0s linear 0.3s;
}
.hx-loader.is-open {
    opacity: 1;
    visibility: visible;
    pointer-events: auto;
    transition: opacity 0.35s var(--ease-hextech), visibility 0s linear 0s;
}

.hx-loader__panel {
    width: 100%;
    max-width: 26rem;
    padding: 2.25rem 2rem 1.9rem;
    text-align: center;
    transform: translateY(10px) scale(0.985);
    transition: transform 0.35s var(--ease-hextech);
}
.hx-loader.is-open .hx-loader__panel {
    transform: none;
}

/* ---- Signature core ---- */
.hx-core {
    position: relative;
    width: 122px;
    height: 122px;
    margin: 0 auto 1.5rem;
}
.hx-core__glow {
    position: absolute;
    inset: -22%;
    background: radial-gradient(closest-side, rgba(10, 200, 185, 0.4), transparent 70%);
    filter: blur(6px);
    animation: hx-breathe-glow 3.2s ease-in-out infinite;
}
.hx-core__svg {
    position: relative;
    width: 100%;
    height: 100%;
    overflow: visible;
}
.hx-core__ring {
    stroke: rgba(200, 170, 110, 0.45);
    stroke-width: 1.5;
}
.hx-core__sweep {
    stroke: var(--color-hex);
    stroke-width: 2.5;
    stroke-linecap: round;
    stroke-dasharray: 22 78;
    filter: drop-shadow(0 0 6px rgba(10, 200, 185, 0.7));
    animation: hx-sweep 1.6s linear infinite;
}
.hx-core__inner {
    fill: rgba(10, 200, 185, 0.06);
    stroke: rgba(200, 170, 110, 0.3);
    stroke-width: 1;
    transform-box: view-box;
    transform-origin: 60px 60px;
    animation: hx-inner 3.2s ease-in-out infinite;
}
.hx-core__orbit {
    fill: var(--color-hex-bright);
    transform-box: view-box;
    transform-origin: 60px 60px;
    animation: hx-spin 6s linear infinite;
}
.hx-core__gem {
    fill: var(--color-gold);
    stroke: var(--color-gold-bright);
    stroke-width: 1;
    transform-box: view-box;
    transform-origin: 60px 60px;
    filter: drop-shadow(0 0 5px rgba(200, 170, 110, 0.6));
    animation: hx-gem 7s linear infinite;
}

/* ---- Text ---- */
.hx-loader__eyebrow {
    margin-bottom: 0.6rem;
}
.hx-loader__title {
    font-family: var(--font-beaufort);
    text-transform: uppercase;
    letter-spacing: 0.08em;
    font-size: 1.4rem;
    color: var(--color-gold-bright);
}
.hx-loader__subtitle {
    margin-top: 0.4rem;
    font-family: var(--font-spiegel);
    font-size: 0.85rem;
    color: var(--color-text-muted);
    min-height: 1.2em;
}

/* ---- Manifest ---- */
.hx-manifest {
    margin: 1.5rem 0 1rem;
    list-style: none;
    padding: 0;
    text-align: left;
}
.hx-row {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    padding: 0.4rem 0;
}
.hx-row__icon {
    display: grid;
    place-items: center;
    width: 1.85rem;
    height: 1.85rem;
    flex: none;
    color: var(--color-gold);
    border: 1px solid rgba(120, 90, 40, 0.5);
    background: rgba(10, 20, 40, 0.6);
}
.hx-row__icon svg {
    width: 1.05rem;
    height: 1.05rem;
}
.hx-row--fetching .hx-row__icon {
    animation: hx-row-pulse 1.5s ease-in-out infinite;
    animation-delay: calc(var(--i) * 0.16s);
}
.hx-row--ready .hx-row__icon {
    color: var(--color-gold-bright);
    border-color: var(--color-gold);
    box-shadow: inset 0 0 0 1px rgba(200, 170, 110, 0.2);
}
.hx-row__label {
    font-family: var(--font-spiegel);
    font-size: 0.9rem;
    color: var(--color-text);
    white-space: nowrap;
}
.hx-row__lead {
    flex: 1;
    align-self: flex-end;
    height: 1px;
    margin: 0 0.35rem 0.4rem;
    border-bottom: 1px dotted rgba(120, 90, 40, 0.5);
}
.hx-row__status {
    display: inline-flex;
    align-items: center;
    gap: 0.3rem;
    flex: none;
    font-family: var(--font-mono);
    font-size: 0.66rem;
    text-transform: uppercase;
    letter-spacing: 0.14em;
    color: var(--color-hex);
}
.hx-row--ready .hx-row__status {
    color: var(--color-gold-bright);
}
.hx-row__check {
    width: 0.85rem;
    height: 0.85rem;
}

/* ---- Live streamed names ---- */
.hx-log {
    list-style: none;
    margin: 0 0 1.1rem;
    padding: 0.55rem 0.7rem;
    min-height: 3.4rem;
    max-height: 3.4rem;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    justify-content: flex-end;
    gap: 0.15rem;
    text-align: left;
    border: 1px solid rgba(120, 90, 40, 0.28);
    background: rgba(4, 12, 24, 0.55);
}
.hx-log__row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-family: var(--font-mono);
    font-size: 0.72rem;
    color: var(--color-text-muted);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    animation: hx-log-in 0.32s var(--ease-hextech);
}
.hx-log__row:last-child {
    color: var(--color-text);
}
.hx-log__name {
    overflow: hidden;
    text-overflow: ellipsis;
}
.hx-log__dot {
    width: 0.4rem;
    height: 0.4rem;
    flex: none;
    border-radius: 1px;
    transform: rotate(45deg);
    background: var(--color-hex);
    box-shadow: 0 0 6px -1px var(--color-hex);
}
.hx-log__dot--items { background: var(--color-gold); box-shadow: 0 0 6px -1px var(--color-gold); }
.hx-log__dot--runes { background: var(--color-hex-bright); box-shadow: 0 0 6px -1px var(--color-hex-bright); }
.hx-log__dot--summoners { background: var(--color-gold-bright); box-shadow: 0 0 6px -1px var(--color-gold-bright); }

/* ---- Determinate energy bar ---- */
.hx-bar {
    position: relative;
    height: 4px;
    overflow: hidden;
    background: rgba(120, 90, 40, 0.18);
    border: 1px solid rgba(120, 90, 40, 0.35);
}
.hx-bar__fill {
    display: block;
    height: 100%;
    width: 0;
    background: linear-gradient(90deg, var(--color-gold), var(--color-hex-bright));
    box-shadow: 0 0 10px -1px rgba(10, 200, 185, 0.7);
    transition: width 0.35s var(--ease-hextech);
}
/* Before the total is known (dataset phase) sweep an indeterminate shimmer. */
.hx-bar--indeterminate .hx-bar__fill {
    width: 40% !important;
    background: linear-gradient(90deg, transparent, var(--color-gold), var(--color-hex), transparent);
    animation: hx-bar-slide 1.3s var(--ease-hextech) infinite;
    transition: none;
}
.hx-bar__pct {
    margin-top: 0.45rem;
    font-family: var(--font-mono);
    font-size: 0.66rem;
    letter-spacing: 0.14em;
    color: var(--color-hex);
}

/* ---- Keyframes ---- */
@keyframes hx-sweep { to { stroke-dashoffset: -100; } }
@keyframes hx-spin { to { transform: rotate(360deg); } }
@keyframes hx-gem {
    0% { transform: rotate(45deg); }
    100% { transform: rotate(405deg); }
}
@keyframes hx-inner {
    0%, 100% { opacity: 0.5; transform: scale(0.97); }
    50% { opacity: 1; transform: scale(1.03); }
}
@keyframes hx-breathe-glow {
    0%, 100% { opacity: 0.55; transform: scale(0.94); }
    50% { opacity: 1; transform: scale(1.06); }
}
@keyframes hx-row-pulse {
    0%, 100% { border-color: rgba(120, 90, 40, 0.5); color: var(--color-gold); box-shadow: none; }
    50% { border-color: var(--color-hex); color: var(--color-hex-bright); box-shadow: 0 0 14px -2px rgba(10, 200, 185, 0.6); }
}
@keyframes hx-bar-slide {
    0% { transform: translateX(-120%); }
    100% { transform: translateX(320%); }
}
@keyframes hx-log-in {
    from { opacity: 0; transform: translateY(4px); }
    to { opacity: 1; transform: none; }
}

@media (prefers-reduced-motion: reduce) {
    .hx-core__sweep,
    .hx-core__inner,
    .hx-core__orbit,
    .hx-core__gem,
    .hx-core__glow,
    .hx-row__icon,
    .hx-log__row,
    .hx-bar--indeterminate .hx-bar__fill { animation: none !important; }
    .hx-loader,
    .hx-loader.is-open { transition: opacity 0.15s linear, visibility 0s; }
    .hx-loader__panel { transform: none !important; transition: none; }
    .hx-bar__fill { transition: none; }
    .hx-bar--indeterminate .hx-bar__fill { width: 100% !important; opacity: 0.6; }
}
</style>
