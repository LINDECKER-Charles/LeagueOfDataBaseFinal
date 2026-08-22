<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { chromaLabel } from '../../chroma/chromaLabel'
import { chromaRamp } from '../../chroma/chromaRamp'

/**
 * Chroma strip island — one per skin that has chromas. Data Dragon exposes no
 * chroma assets (only a boolean flag), so the colours + preview art come from
 * CommunityDragon (resolved server-side, hotlinked here). Renders the swatch row
 * and owns a zoom modal; all listeners are scoped to the component (keydown only
 * while open) so nothing leaks across Turbo visits.
 */
interface Chroma {
    id: number
    name: string
    colors: string[]
    image: string
}

const props = defineProps<{ skinName: string; chromas: Chroma[] }>()

const openIndex = ref<number | null>(null)
const current = computed(
    () => (openIndex.value === null ? null : props.chromas[openIndex.value] ?? null),
)

function open(i: number): void {
    openIndex.value = i
}
function close(): void {
    openIndex.value = null
}
function step(delta: number): void {
    if (openIndex.value === null) return
    const n = props.chromas.length
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
    <div class="chroma-strip mt-2 flex flex-wrap gap-1.5">
        <button
            v-for="(c, i) in chromas"
            :key="c.id"
            type="button"
            class="chroma-swatch"
            :title="chromaLabel(c)"
            :aria-label="chromaLabel(c)"
            @click="open(i)"
        >
            <span
                class="chroma-swatch__ring"
                :style="{ background: chromaRamp(c.colors) }"
                aria-hidden="true"
            />
            <img class="hx-img"
                :src="c.image"
                :alt="chromaLabel(c)"
                loading="lazy"
                decoding="async"
                width="34"
                height="34"
            />
        </button>
    </div>

    <Teleport to="body">
        <div v-if="current" class="fixed inset-0 z-[60] grid place-items-center p-4">
            <div class="absolute inset-0 bg-hextech-black/75 backdrop-blur-sm" @click="close" />

            <div
                class="hextech-frame relative z-10 w-[min(92vw,30rem)] overflow-hidden"
                role="dialog"
                aria-modal="true"
            >
                <div
                    class="flex items-center justify-between gap-4 border-b border-gold-deep/40
                           px-5 py-3"
                >
                    <div class="min-w-0">
                        <p class="eyebrow">{{ skinName }}</p>
                        <h2 class="truncate font-beaufort uppercase tracking-wide text-gold-bright">
                            {{ chromaLabel(current) }}
                        </h2>
                    </div>
                    <button
                        type="button"
                        class="shrink-0 rounded p-1 text-text-muted hover:text-gold-bright"
                        aria-label="Close"
                        @click="close"
                    >
                        <svg
                            class="h-5 w-5"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="2"
                        >
                            <path d="M18 6L6 18M6 6l12 12" />
                        </svg>
                    </button>
                </div>

                <div class="relative grid place-items-center bg-void/60 p-6">
                    <img
                        :key="current.id"
                        :src="current.image"
                        :alt="chromaLabel(current)"
                        class="hx-img chroma-modal__art h-64 w-64 max-w-full object-contain"
                    />

                    <template v-if="chromas.length > 1">
                        <button
                            type="button"
                            class="chroma-nav chroma-nav--prev"
                            aria-label="Previous"
                            @click="step(-1)"
                        >
                            <svg
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                stroke-width="2"
                            >
                                <path d="M15 6l-6 6 6 6" />
                            </svg>
                        </button>
                        <button
                            type="button"
                            class="chroma-nav chroma-nav--next"
                            aria-label="Next"
                            @click="step(1)"
                        >
                            <svg
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                stroke-width="2"
                            >
                                <path d="M9 6l6 6-6 6" />
                            </svg>
                        </button>
                    </template>
                </div>

                <div
                    class="flex items-center justify-between gap-3 border-t border-gold-deep/40
                           px-5 py-3"
                >
                    <div class="flex items-center gap-1.5">
                        <span
                            v-for="(col, k) in current.colors"
                            :key="k"
                            class="h-4 w-4 rounded-sm border border-gold-deep/50"
                            :style="{ background: col }"
                            :title="col"
                        />
                    </div>
                    <span v-if="chromas.length > 1" class="font-mono text-xs text-text-dim">
                        {{ (openIndex ?? 0) + 1 }} / {{ chromas.length }}
                    </span>
                </div>
            </div>
        </div>
    </Teleport>
</template>

<style scoped>
.chroma-swatch {
    position: relative;
    display: grid;
    place-items: center;
    width: 2.5rem;
    height: 2.5rem;
    padding: 0;
    border: 1px solid var(--color-gold-deep);
    background: var(--color-void, #0a1428);
    cursor: pointer;
    transition:
        border-color 0.2s var(--ease-hextech, ease),
        transform 0.2s var(--ease-hextech, ease);
}
.chroma-swatch:hover,
.chroma-swatch:focus-visible {
    border-color: var(--color-gold);
    transform: translateY(-1px);
    outline: none;
}
.chroma-swatch__ring {
    position: absolute;
    inset: 0;
    opacity: 0.28;
}
.chroma-swatch img {
    position: relative;
    width: 1.85rem;
    height: 1.85rem;
    object-fit: contain;
    filter: drop-shadow(0 1px 2px rgba(0, 0, 0, 0.6));
}
.chroma-swatch:hover .chroma-swatch__ring {
    opacity: 0.45;
}

.chroma-modal__art {
    filter: drop-shadow(0 6px 18px rgba(0, 0, 0, 0.55));
    animation: chroma-pop 0.24s var(--ease-hextech, ease);
}

.chroma-nav {
    position: absolute;
    top: 50%;
    display: grid;
    place-items: center;
    width: 2.2rem;
    height: 2.2rem;
    transform: translateY(-50%);
    color: var(--color-gold);
    border: 1px solid var(--color-gold-deep);
    background: rgba(4, 12, 24, 0.72);
    transition: color 0.2s ease, border-color 0.2s ease;
}
.chroma-nav:hover {
    color: var(--color-gold-bright);
    border-color: var(--color-gold);
}
.chroma-nav svg {
    width: 1.15rem;
    height: 1.15rem;
}
.chroma-nav--prev { left: 0.6rem; }
.chroma-nav--next { right: 0.6rem; }

@keyframes chroma-pop {
    from { opacity: 0; transform: scale(0.96); }
    to { opacity: 1; transform: none; }
}
@media (prefers-reduced-motion: reduce) {
    .chroma-modal__art { animation: none; }
    .chroma-swatch { transition: none; }
    .chroma-swatch:hover { transform: none; }
}
</style>
