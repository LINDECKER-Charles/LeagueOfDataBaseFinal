import { computed, onBeforeUnmount, ref, type ComputedRef, type Ref } from 'vue'

/**
 * Minimal state machine over native HTML5 drag events, reusable for any
 * (source, target) pair: the caller decides what is draggable, computes drop
 * targets during dragover (typically an insertion index from pointer geometry)
 * and receives a single onCommit(source, target) when a drop lands.
 *
 * Cancellation: Escape aborts the native drag (the browser then fires dragend
 * without a drop) and `end()` clears the state; a document-level Escape
 * listener also cancels directly, which covers non-native environments (tests).
 * The composable owns NO announcement/aria concern — callers announce on commit.
 */

export interface DragReorderOptions<S, T> {
    onCommit: (source: S, target: T) => void
    onCancel?: () => void
}

export interface DragReorder<S, T> {
    source: Ref<S | null>
    target: Ref<T | null>
    isDragging: ComputedRef<boolean>
    start: (payload: S, event: DragEvent) => void
    over: (candidate: T, event: DragEvent) => void
    leave: (candidate: T) => void
    drop: (event: DragEvent) => void
    end: () => void
    cancel: () => void
}

/** What one drag in flight is made of; the transitions below act on it. */
interface DragState<S, T> {
    source: Ref<S | null>
    target: Ref<T | null>
    options: DragReorderOptions<S, T>
    escape: EscapeGuard
}

interface EscapeGuard {
    arm: () => void
    disarm: () => void
}

export function useDragReorder<S, T>(options: DragReorderOptions<S, T>): DragReorder<S, T> {
    const state: DragState<S, T> = {
        source: ref(null) as Ref<S | null>,
        target: ref(null) as Ref<T | null>,
        options,
        escape: useEscapeGuard(() => cancel(state)),
    }

    return {
        source: state.source,
        target: state.target,
        isDragging: computed(() => state.source.value !== null),
        start: (payload, event) => start(state, payload, event),
        over: (candidate, event) => over(state, candidate, event),
        leave: (candidate) => leave(state, candidate),
        drop: (event) => drop(state, event),
        end: () => end(state),
        cancel: () => cancel(state),
    }
}

/** Document-level Escape listener, armed only while a drag is in flight. */
function useEscapeGuard(onEscape: () => void): EscapeGuard {
    const onKeydown = (event: KeyboardEvent): void => {
        if (event.key === 'Escape') onEscape()
    }
    onBeforeUnmount(() => document.removeEventListener('keydown', onKeydown))

    return {
        arm: () => document.addEventListener('keydown', onKeydown),
        disarm: () => document.removeEventListener('keydown', onKeydown),
    }
}

function start<S, T>(state: DragState<S, T>, payload: S, event: DragEvent): void {
    state.source.value = payload
    state.target.value = null
    state.escape.arm()
    if (event.dataTransfer) {
        event.dataTransfer.effectAllowed = 'move'
        // Firefox refuses to start a drag with an empty data store.
        event.dataTransfer.setData('text/plain', 'drag')
    }
}

function over<S, T>(state: DragState<S, T>, candidate: T, event: DragEvent): void {
    if (state.source.value === null) return
    // preventDefault marks the zone as a valid drop target (HTML5 contract).
    event.preventDefault()
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
    state.target.value = candidate
}

function leave<S, T>(state: DragState<S, T>, candidate: T): void {
    if (JSON.stringify(state.target.value) === JSON.stringify(candidate)) {
        state.target.value = null
    }
}

function drop<S, T>(state: DragState<S, T>, event: DragEvent): void {
    event.preventDefault()
    const from = state.source.value
    const to = state.target.value
    reset(state)
    if (from !== null && to !== null) state.options.onCommit(from, to)
}

/** dragend fires after BOTH drop and abort; a still-armed source means abort. */
function end<S, T>(state: DragState<S, T>): void {
    if (state.source.value !== null) cancel(state)
}

function cancel<S, T>(state: DragState<S, T>): void {
    if (state.source.value === null) return
    reset(state)
    state.options.onCancel?.()
}

function reset<S, T>(state: DragState<S, T>): void {
    state.source.value = null
    state.target.value = null
    state.escape.disarm()
}
