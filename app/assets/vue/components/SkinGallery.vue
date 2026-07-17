<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import ChromaStrip from './ChromaStrip.vue'

/**
 * Skin gallery island — the champion's alternate skins as a horizontal strip of
 * tiles, each opening a full-splash lightbox (contain-fit, keyboard-navigable).
 * Chroma variants are surfaced by the embedded {@see ChromaStrip}, never as tiles
 * of their own (Data Dragon inlines them as skins; they are filtered server-side).
 * Listeners are scoped to the open state so nothing leaks across Turbo visits.
 */
interface Chroma {
    id: number
    name: string
    colors: string[]
    image: string
}
interface Skin {
    num: number
    name: string
    splash: string
    chromas: Chroma[]
}

const props = defineProps<{ championName: string; skins: Skin[] }>()

const openIndex = ref<number | null>(null)
const current = computed(() => (openIndex.value === null ? null : props.skins[openIndex.value] ?? null))

function open(i: number): void {
    openIndex.value = i
}
function close(): void {
    openIndex.value = null
}
function step(delta: number): void {
    if (openIndex.value === null) return
    const n = props.skins.length
    openIndex.value = (openIndex.value + delta + n) % n
}

function onKey(e: KeyboardEvent): void {
    if (openIndex.value === null) return
    if (e.key === 'Escape') close()
    else if (e.key === 'ArrowRight') step(1)
    else if (e.key === 'ArrowLeft') step(-1)
}

watch(openIndex, (v) => {
    if (v === null) document.removeEventListener('keydown', onKey)
    else document.addEventListener('keydown', onKey)
})
onBeforeUnmount(() => document.removeEventListener('keydown', onKey))
</script>

<template>
    <div class="-mx-6 flex snap-x snap-proximity gap-4 overflow-x-auto px-6 pb-2">
        <figure v-for="(skin, i) in skins" :key="skin.num" class="w-72 shrink-0 snap-start scroll-ml-6">
            <button
                type="button"
                class="skin-tile hextech-frame relative block w-full overflow-hidden"
                :aria-label="skin.name"
                @click="open(i)"
            >
                <img
                    :src="skin.splash"
                    :alt="skin.name"
                    loading="lazy"
                    decoding="async"
                    class="h-36 w-full object-cover object-top"
                />
                <span
                    v-if="skin.chromas.length"
                    class="chroma-badge"
                    :title="`${skin.chromas.length}`"
                >
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true">
                        <path d="M12 3 21 12 12 21 3 12z" /><path d="M7.5 12 12 7.5 16.5 12 12 16.5z" />
                    </svg>
                    {{ skin.chromas.length }}
                </span>
                <span class="skin-tile__zoom" aria-hidden="true">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
                        <circle cx="11" cy="11" r="7" /><path d="M21 21l-4.3-4.3M11 8v6M8 11h6" />
                    </svg>
                </span>
            </button>
            <figcaption class="mt-2 font-mono text-xs text-text-muted">{{ skin.name }}</figcaption>
            <ChromaStrip v-if="skin.chromas.length" :skin-name="skin.name" :chromas="skin.chromas" />
        </figure>
    </div>

    <Teleport to="body">
        <div v-if="current" class="fixed inset-0 z-[60] grid place-items-center p-4">
            <div class="absolute inset-0 bg-hextech-black/80 backdrop-blur-sm" @click="close" />

            <figure class="relative z-10 w-[min(94vw,72rem)]" role="dialog" aria-modal="true">
                <div class="relative">
                    <img
                        :key="current.num"
                        :src="current.splash"
                        :alt="current.name"
                        class="skin-splash mx-auto max-h-[80vh] w-full rounded object-contain"
                    />

                    <button type="button" class="skin-close" aria-label="Close" @click="close">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 6L6 18M6 6l12 12" /></svg>
                    </button>

                    <template v-if="skins.length > 1">
                        <button type="button" class="skin-nav skin-nav--prev" aria-label="Previous" @click="step(-1)">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M15 6l-6 6 6 6" /></svg>
                        </button>
                        <button type="button" class="skin-nav skin-nav--next" aria-label="Next" @click="step(1)">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 6l6 6-6 6" /></svg>
                        </button>
                    </template>
                </div>

                <figcaption class="mt-3 flex items-center justify-between gap-4 px-1">
                    <div class="min-w-0">
                        <p class="eyebrow">{{ championName }}</p>
                        <h2 class="truncate font-beaufort uppercase tracking-wide text-gold-bright">{{ current.name }}</h2>
                    </div>
                    <span v-if="skins.length > 1" class="shrink-0 font-mono text-xs text-text-dim">
                        {{ (openIndex ?? 0) + 1 }} / {{ skins.length }}
                    </span>
                </figcaption>
            </figure>
        </div>
    </Teleport>
</template>

<style scoped>
.skin-tile {
    padding: 0;
    cursor: pointer;
    transition: transform 0.2s var(--ease-hextech, ease), border-color 0.2s var(--ease-hextech, ease);
}
.skin-tile:hover,
.skin-tile:focus-visible {
    transform: translateY(-2px);
    outline: none;
}
.skin-tile img {
    transition: transform 0.35s var(--ease-hextech, ease);
}
.skin-tile:hover img {
    transform: scale(1.05);
}

.skin-tile__zoom {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    color: var(--color-gold-bright);
    background: rgba(4, 12, 24, 0.35);
    opacity: 0;
    transition: opacity 0.2s var(--ease-hextech, ease);
}
.skin-tile__zoom svg {
    width: 2rem;
    height: 2rem;
    filter: drop-shadow(0 2px 6px rgba(0, 0, 0, 0.7));
}
.skin-tile:hover .skin-tile__zoom,
.skin-tile:focus-visible .skin-tile__zoom {
    opacity: 1;
}

.skin-splash {
    filter: drop-shadow(0 10px 30px rgba(0, 0, 0, 0.6));
    animation: skin-pop 0.24s var(--ease-hextech, ease);
}

.skin-close {
    position: absolute;
    top: 0.6rem;
    right: 0.6rem;
    display: grid;
    place-items: center;
    width: 2.2rem;
    height: 2.2rem;
    color: var(--color-gold);
    border: 1px solid var(--color-gold-deep);
    background: rgba(4, 12, 24, 0.72);
    transition: color 0.2s ease, border-color 0.2s ease;
}
.skin-close:hover {
    color: var(--color-gold-bright);
    border-color: var(--color-gold);
}
.skin-close svg {
    width: 1.15rem;
    height: 1.15rem;
}

.skin-nav {
    position: absolute;
    top: 50%;
    display: grid;
    place-items: center;
    width: 2.6rem;
    height: 2.6rem;
    transform: translateY(-50%);
    color: var(--color-gold);
    border: 1px solid var(--color-gold-deep);
    background: rgba(4, 12, 24, 0.72);
    transition: color 0.2s ease, border-color 0.2s ease;
}
.skin-nav:hover {
    color: var(--color-gold-bright);
    border-color: var(--color-gold);
}
.skin-nav svg {
    width: 1.3rem;
    height: 1.3rem;
}
.skin-nav--prev { left: 0.6rem; }
.skin-nav--next { right: 0.6rem; }

@keyframes skin-pop {
    from { opacity: 0; transform: scale(0.98); }
    to { opacity: 1; transform: none; }
}
@media (prefers-reduced-motion: reduce) {
    .skin-tile,
    .skin-tile img,
    .skin-tile__zoom,
    .skin-splash {
        transition: none;
        animation: none;
    }
    .skin-tile:hover,
    .skin-tile:hover img {
        transform: none;
    }
}
</style>
