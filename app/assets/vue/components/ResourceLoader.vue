<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'

/**
 * Global navigation loader island — persists across Turbo visits via
 * `data-turbo-permanent`. Cold version loads make Symfony ingest the whole
 * DDragon dataset into MinIO (multi-second first render); this overlay names the
 * resources the destination page is about to load instead of freezing silently.
 *
 * Honesty note: statuses mirror the real navigation lifecycle only —
 * "fetching" while the Turbo visit is in flight, "ready" once turbo:load fires.
 * No fabricated percentage. Warm visits (~150 ms) never reach the show delay.
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
    labels: Labels
    status: { fetching: string; ready: string }
}>()

type ResourceKey = keyof Labels

/** Only show for resource-bearing destinations; nav overhead should stay invisible elsewhere. */
function resourcesFor(pathname: string): ResourceKey[] {
    const p = (pathname.replace(/\/+$/, '') || '/').toLowerCase()
    if (p === '/home') return ['champions', 'items', 'runes', 'summoners']
    if (/^\/champions?(?:_redirect)?(?:\/|$)/.test(p)) return ['champions']
    if (/^\/objects?(?:_redirect)?(?:\/|$)/.test(p)) return ['items']
    if (/^\/runes?(?:_redirect)?(?:\/|$)/.test(p)) return ['runes']
    if (/^\/summoners?(?:_redirect)?(?:\/|$)/.test(p)) return ['summoners']
    return []
}

const SHOW_DELAY = 280 // warm visits resolve first and never flash the overlay
const MIN_VISIBLE = 520 // once shown, hold it long enough to read

const visible = ref(false)
const finishing = ref(false)
const active = ref<ResourceKey[]>([])

let showTimer: ReturnType<typeof setTimeout> | undefined
let hideTimer: ReturnType<typeof setTimeout> | undefined
let pending: ResourceKey[] = []
let shownAt = 0

function scheduleShow(keys: ResourceKey[]): void {
    if (!keys.length) return
    pending = keys
    if (hideTimer) { clearTimeout(hideTimer); hideTimer = undefined }
    if (visible.value) { active.value = keys; finishing.value = false; return }
    if (showTimer) return
    showTimer = setTimeout(() => {
        showTimer = undefined
        active.value = pending
        finishing.value = false
        visible.value = true
        shownAt = performance.now()
    }, SHOW_DELAY)
}

function scheduleHide(): void {
    if (showTimer) { clearTimeout(showTimer); showTimer = undefined }
    if (!visible.value || hideTimer) return
    finishing.value = true
    const wait = Math.max(0, MIN_VISIBLE - (performance.now() - shownAt))
    hideTimer = setTimeout(() => {
        hideTimer = undefined
        visible.value = false
        finishing.value = false
    }, wait + 280)
}

function onVisit(e: Event): void {
    const url = (e as CustomEvent<{ url?: string }>).detail?.url
    const path = url ? new URL(url, window.location.origin).pathname : window.location.pathname
    scheduleShow(resourcesFor(path))
}
// Form submits (header version/language switch) redirect back to the current page.
function onSubmitStart(): void {
    scheduleShow(resourcesFor(window.location.pathname))
}
function onLoad(): void {
    scheduleHide()
}
function onSubmitEnd(e: Event): void {
    if ((e as CustomEvent<{ success?: boolean }>).detail?.success === false) scheduleHide()
}

onMounted(() => {
    document.addEventListener('turbo:visit', onVisit)
    document.addEventListener('turbo:submit-start', onSubmitStart)
    document.addEventListener('turbo:load', onLoad)
    document.addEventListener('turbo:submit-end', onSubmitEnd)
    document.addEventListener('turbo:fetch-request-error', onLoad)
})
onBeforeUnmount(() => {
    document.removeEventListener('turbo:visit', onVisit)
    document.removeEventListener('turbo:submit-start', onSubmitStart)
    document.removeEventListener('turbo:load', onLoad)
    document.removeEventListener('turbo:submit-end', onSubmitEnd)
    document.removeEventListener('turbo:fetch-request-error', onLoad)
    if (showTimer) clearTimeout(showTimer)
    if (hideTimer) clearTimeout(hideTimer)
})
</script>

<template>
    <Transition name="hx-loader">
        <div
            v-show="visible"
            class="hx-loader"
            role="status"
            aria-live="polite"
            :aria-busy="!finishing"
        >
            <div class="hx-loader__panel hextech-frame">
                <!-- Signature: charging Hextech core -->
                <div class="hx-core" aria-hidden="true">
                    <span class="hx-core__glow"></span>
                    <svg viewBox="0 0 120 120" class="hx-core__svg">
                        <polygon
                            class="hx-core__ring"
                            points="112,60 86,105 34,105 8,60 34,15 86,15"
                            fill="none"
                        />
                        <polygon
                            class="hx-core__sweep"
                            points="112,60 86,105 34,105 8,60 34,15 86,15"
                            fill="none"
                            pathLength="100"
                        />
                        <polygon
                            class="hx-core__inner"
                            points="98,60 79,93 41,93 22,60 41,27 79,27"
                        />
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
                <p class="hx-loader__subtitle">{{ subtitle }}</p>

                <!-- The manifest: resources the destination page is loading -->
                <ul class="hx-manifest">
                    <li
                        v-for="(key, i) in active"
                        :key="key"
                        class="hx-row"
                        :class="finishing ? 'hx-row--ready' : 'hx-row--fetching'"
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
                            <svg v-if="finishing" class="hx-row__check" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
                                <path d="M3 8.5 6.5 12 13 4.5" />
                            </svg>
                            {{ finishing ? status.ready : status.fetching }}
                        </span>
                    </li>
                </ul>

                <div class="hx-bar" aria-hidden="true"><span class="hx-bar__seg"></span></div>
            </div>
        </div>
    </Transition>
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
}

.hx-loader__panel {
    width: 100%;
    max-width: 26rem;
    padding: 2.25rem 2rem 1.9rem;
    text-align: center;
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
}

/* ---- Manifest ---- */
.hx-manifest {
    margin: 1.6rem 0 1.4rem;
    list-style: none;
    padding: 0;
    text-align: left;
}
.hx-row {
    display: flex;
    align-items: center;
    gap: 0.6rem;
    padding: 0.42rem 0;
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

/* ---- Indeterminate energy bar ---- */
.hx-bar {
    position: relative;
    height: 3px;
    overflow: hidden;
    background: rgba(120, 90, 40, 0.18);
    border: 1px solid rgba(120, 90, 40, 0.35);
}
.hx-bar__seg {
    position: absolute;
    inset-block: 0;
    left: -40%;
    width: 40%;
    background: linear-gradient(90deg, transparent, var(--color-gold), var(--color-hex), transparent);
    animation: hx-bar-slide 1.3s var(--ease-hextech) infinite;
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
    0% { left: -40%; }
    100% { left: 100%; }
}

/* ---- Enter / leave ---- */
.hx-loader-enter-active,
.hx-loader-leave-active { transition: opacity 0.3s var(--ease-hextech); }
.hx-loader-enter-from,
.hx-loader-leave-to { opacity: 0; }
.hx-loader-enter-active .hx-loader__panel { transition: transform 0.35s var(--ease-hextech), opacity 0.35s var(--ease-hextech); }
.hx-loader-enter-from .hx-loader__panel { transform: translateY(10px) scale(0.98); opacity: 0; }

@media (prefers-reduced-motion: reduce) {
    .hx-core__sweep,
    .hx-core__inner,
    .hx-core__orbit,
    .hx-core__gem,
    .hx-core__glow,
    .hx-row__icon,
    .hx-bar__seg { animation: none !important; }
    .hx-loader-enter-active,
    .hx-loader-leave-active,
    .hx-loader-enter-active .hx-loader__panel { transition: opacity 0.2s linear; }
    .hx-bar__seg { left: 0; width: 100%; opacity: 0.6; }
}
</style>
