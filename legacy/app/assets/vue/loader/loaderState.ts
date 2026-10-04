import { computed, ref, type ComputedRef, type Ref } from 'vue'
import type { Phase, ResourceKey } from './urls'

/*
 * Only the newest entry is surfaced, but a short tail is kept so the panel can
 * grow a history without re-plumbing the stream. Trimming in one go (rather
 * than shifting on every item) keeps the hot `item` handler allocation-free.
 */
const LIVE_BUFFER_MAX = 18
const LIVE_BUFFER_KEEP = 12

/** One resource the stream reported as stored, as shown by the live line. */
export interface LoaderEntry {
    id: number
    name: string
    key: ResourceKey
}

/** Reactive surface of the overlay — the only thing the island binds to. */
export interface LoaderState {
    visible: Ref<boolean>
    finishing: Ref<boolean>
    phase: Ref<Phase>
    progress: Ref<number>
    active: Ref<ResourceKey[]>
    readyKeys: Ref<ResourceKey[]>
    entries: Ref<LoaderEntry[]>
    current: ComputedRef<LoaderEntry | null>
    pct: ComputedRef<number>
}

// Entry ids only need to be unique inside one buffer; a module-wide counter is
// the cheapest monotonic source and never collides across runs.
let entrySeq = 0

export function createLoaderState(): LoaderState {
    const progress = ref(0)
    const entries = ref<LoaderEntry[]>([])

    return {
        visible: ref(false),
        finishing: ref(false),
        phase: ref<Phase>('idle'),
        progress,
        active: ref<ResourceKey[]>([]),
        readyKeys: ref<ResourceKey[]>([]),
        entries,
        current: computed(() => entries.value.at(-1) ?? null),
        pct: computed(() => Math.min(100, Math.round(progress.value * 100))),
    }
}

/** Arm the overlay for a fresh run over `resources`. */
export function beginRun(state: LoaderState, resources: ResourceKey[]): void {
    state.progress.value = 0
    state.phase.value = 'preparing'
    state.finishing.value = false
    state.active.value = resources
    state.readyKeys.value = []
    state.entries.value = []
}

/** Terminal beat of a run: full bar, every manifest row ready. */
export function completeRun(state: LoaderState): void {
    state.progress.value = 1
    state.phase.value = 'done'
    state.finishing.value = true
    state.active.value.forEach((key) => markReady(state, key))
}

/** Back to hidden/idle (in-place warm end, or once a gated visit lands). */
export function hideOverlay(state: LoaderState): void {
    state.visible.value = false
    state.finishing.value = false
    state.phase.value = 'idle'
}

export function markReady(state: LoaderState, key: ResourceKey): void {
    if (!state.readyKeys.value.includes(key)) {
        state.readyKeys.value = [...state.readyKeys.value, key]
    }
}

export function appendEntry(state: LoaderState, name: string, key: ResourceKey): void {
    state.entries.value.push({ id: ++entrySeq, name, key })
    if (state.entries.value.length > LIVE_BUFFER_MAX) {
        state.entries.value = state.entries.value.slice(-LIVE_BUFFER_KEEP)
    }
}
