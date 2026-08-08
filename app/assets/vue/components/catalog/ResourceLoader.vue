<script setup lang="ts">
import LoaderCore from '../../loader/LoaderCore.vue'
import { useLoaderStream } from '../../loader/useLoaderStream'
import type { Labels } from '../../loader/urls'

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
 * The SSE state machine + Turbo wiring live in {@link useLoaderStream}; this SFC
 * is presentation only. Honesty note: the bar reflects images actually stored
 * into object storage (`index/total` from the stream), and "ready" fires on the
 * real `done` event — no fabricated percentage.
 */
defineProps<{
    eyebrow?: string
    title?: string
    subtitle?: string
    preparing?: string
    labels: Labels
    status: { fetching: string; ready: string }
}>()

const { visible, finishing, phase, progress, active, readyKeys, entries, current, pct } =
    useLoaderStream()

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
            <LoaderCore />

            <p class="hx-loader__eyebrow eyebrow">{{ eyebrow }}</p>
            <h2 class="hx-loader__title">{{ title }}</h2>
            <p
                class="hx-loader__subtitle"
            >{{ phase === 'preparing' ? (preparing ?? subtitle) : subtitle }}</p>

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
                        <svg
                            v-if="key === 'champions'"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="1.6"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                        >
                            <path d="M14.5 4 20 4l0 5.5" />
                            <path d="M20 4 4 20" />
                            <path d="M9.5 20 4 20l0-5.5" />
                            <path d="M4 4l5 5" />
                            <path d="M20 20l-5-5" />
                        </svg>
                        <svg
                            v-else-if="key === 'items'"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="1.6"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                        >
                            <path d="M12 2 21 7v10l-9 5-9-5V7z" />
                            <path d="M12 12 21 7" />
                            <path d="M12 12v10" />
                            <path d="M12 12 3 7" />
                        </svg>
                        <svg
                            v-else-if="key === 'runes'"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="1.6"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                        >
                            <circle cx="12" cy="12" r="8.5" />
                            <path d="M12 3.5v17" />
                            <path d="M12 12 18 8" />
                            <path d="M12 12 6 8" />
                        </svg>
                        <svg
                            v-else
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="1.6"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                        >
                            <path
                                d="M12 3c1.5 3-1 4.5 0 7 2.5-1 3-3.5 2-5.5C17.5 6 19 9 19 12a7 7 0 1
                                   1-13.4-2.8C6.4 11 7.8 11.6 9 11c-1.2-2.4.3-6.5 3-8z"
                            />
                        </svg>
                    </span>
                    <span class="hx-row__label">{{ labels[key] }}</span>
                    <span class="hx-row__lead" aria-hidden="true"></span>
                    <span class="hx-row__status">
                        <svg
                            v-if="readyKeys.includes(key)"
                            class="hx-row__check"
                            viewBox="0 0 16 16"
                            fill="none"
                            stroke="currentColor"
                            stroke-width="2.2"
                            stroke-linecap="round"
                            stroke-linejoin="round"
                        >
                            <path d="M3 8.5 6.5 12 13 4.5" />
                        </svg>
                        {{ readyKeys.includes(key) ? status.ready : status.fetching }}
                    </span>
                </li>
            </ul>

            <!-- Live feed: the single resource landing right now. Names stream far
                 too fast to read as a scrolling log — one prominent, always-legible
                 line (name + its category) reads at any speed and never crushes.
                 Visual only — aria-hidden so screen readers aren't flooded. -->
            <div class="hx-now" aria-hidden="true">
                <span
                    class="hx-now__dot"
                    :class="current ? 'hx-now__dot--' + current.key : 'hx-now__dot--idle'"
                ></span>
                <span
                    :key="current?.id ?? 'idle'"
                    class="hx-now__name"
                    :class="{ 'is-idle': !current }"
                >
                    {{ current ? current.name : '' }}
                </span>
                <span v-if="current" class="hx-now__cat">{{ labels[current.key] }}</span>
            </div>

            <!-- Determinate progress bar -->
            <div class="hx-bar" :class="{ 'hx-bar--indeterminate': phase === 'preparing' }">
                <span class="hx-bar__fill" :style="{ width: pct + '%' }"></span>
            </div>
            <p class="hx-bar__pct" aria-hidden="true">{{ pct }}%</p>
        </div>
    </div>
</template>

<style scoped src="./ResourceLoader.css"></style>
