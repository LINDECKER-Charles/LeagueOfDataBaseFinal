<script setup lang="ts">
import { useLoadTiming } from '../timing/useLoadTiming'

/**
 * Persistent load-time badge (base.html.twig, `data-turbo-permanent`). Renders a
 * discreet Hextech chip on detail pages only — it self-hides on any page that
 * carries no `[data-load-timing]` marker. Presentation only; the timing state
 * machine lives in {@link useLoadTiming}.
 */
defineProps<{ labels: { title: string; server: string; client: string } }>()

const { visible, serverMs, clientMs } = useLoadTiming()

// Test seam: specs assert on the state machine directly.
defineExpose({ visible, serverMs, clientMs })

const fmt = (ms: number | null): string => (ms == null ? '—' : `${Math.round(ms)} ms`)
</script>

<template>
    <div v-if="visible" class="hx-perf" role="status" :aria-label="labels.title">
        <span class="hx-perf__spark" aria-hidden="true"></span>
        <span class="hx-perf__pair">
            <span class="hx-perf__k">{{ labels.server }}</span>
            <span class="hx-perf__v">{{ fmt(serverMs) }}</span>
        </span>
        <span class="hx-perf__sep" aria-hidden="true">·</span>
        <span class="hx-perf__pair">
            <span class="hx-perf__k">{{ labels.client }}</span>
            <span class="hx-perf__v">{{ fmt(clientMs) }}</span>
        </span>
    </div>
</template>

<style scoped>
.hx-perf {
    position: fixed;
    right: 1rem;
    bottom: 1rem;
    z-index: 40; /* under the loader overlay (120) and toasts (50) — and a free corner */
    display: inline-flex;
    align-items: baseline;
    gap: 0.5rem;
    padding: 0.4rem 0.7rem;
    font-family: var(--font-mono);
    font-size: 0.66rem;
    letter-spacing: 0.08em;
    color: var(--color-text-muted);
    background: rgba(4, 12, 24, 0.82);
    border: 1px solid rgba(120, 90, 40, 0.4);
    backdrop-filter: blur(4px);
    -webkit-backdrop-filter: blur(4px);
    /* Diagnostic overlay: never intercept clicks on the content beneath it. */
    pointer-events: none;
    user-select: none;
}
.hx-perf__spark {
    align-self: center;
    width: 0.42rem;
    height: 0.42rem;
    transform: rotate(45deg);
    background: var(--color-hex-bright);
    box-shadow: 0 0 7px 0 var(--color-hex);
}
.hx-perf__pair {
    display: inline-flex;
    align-items: baseline;
    gap: 0.3rem;
}
.hx-perf__k {
    text-transform: uppercase;
    color: var(--color-gold);
    opacity: 0.85;
}
.hx-perf__v {
    color: var(--color-hex-bright);
}
.hx-perf__sep {
    color: var(--color-gold-deep);
}

@media (max-width: 640px) {
    .hx-perf {
        right: 0.6rem;
        bottom: 0.6rem;
        padding: 0.3rem 0.55rem;
        font-size: 0.6rem;
    }
}
</style>
