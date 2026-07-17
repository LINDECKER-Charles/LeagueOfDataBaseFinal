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

export function useDragReorder<S, T>(options: DragReorderOptions<S, T>): DragReorder<S, T> {
    const source = ref(null) as Ref<S | null>
    const target = ref(null) as Ref<T | null>
    const isDragging = computed(() => source.value !== null)

    function onKeydown(event: KeyboardEvent): void {
        if (event.key === 'Escape') cancel()
    }

    function reset(): void {
        source.value = null
        target.value = null
        document.removeEventListener('keydown', onKeydown)
    }

    function start(payload: S, event: DragEvent): void {
        source.value = payload
        target.value = null
        document.addEventListener('keydown', onKeydown)
        if (event.dataTransfer) {
            event.dataTransfer.effectAllowed = 'move'
            // Firefox refuses to start a drag with an empty data store.
            event.dataTransfer.setData('text/plain', 'drag')
        }
    }

    function over(candidate: T, event: DragEvent): void {
        if (source.value === null) return
        // preventDefault marks the zone as a valid drop target (HTML5 contract).
        event.preventDefault()
        if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
        target.value = candidate
    }

    function leave(candidate: T): void {
        if (JSON.stringify(target.value) === JSON.stringify(candidate)) target.value = null
    }

    function drop(event: DragEvent): void {
        event.preventDefault()
        const from = source.value
        const to = target.value
        reset()
        if (from !== null && to !== null) options.onCommit(from, to)
    }

    /** dragend fires after BOTH drop and abort; a still-armed source means abort. */
    function end(): void {
        if (source.value !== null) cancel()
    }

    function cancel(): void {
        if (source.value === null) return
        reset()
        options.onCancel?.()
    }

    onBeforeUnmount(() => document.removeEventListener('keydown', onKeydown))

    return { source, target, isDragging, start, over, leave, drop, end, cancel }
}
