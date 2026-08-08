import type { ItemLocation } from './stepList'
import type { BuildStep } from '../editor/structure'
import { useDragReorder, type DragReorder } from './useDragReorder'

/**
 * Drag-and-drop orchestration of the purchase order: it turns native drag
 * events into the (source, target) pairs the pure step rules understand, and
 * answers the "is this being dragged / does it land here?" questions the
 * template renders. Reordering stays a PROGRESSIVE enhancement over the ↑↓/‹›
 * buttons — this composable owns none of the mutations, it only reports moves.
 */
type DragSource = { kind: 'step'; index: number } | { kind: 'item'; step: number; index: number }

type DropTarget = { kind: 'step'; insert: number } | { kind: 'item'; step: number; insert: number }

/** What the drag reads back from the editor to answer capacity questions. */
export interface StepDragSource {
    steps: BuildStep[]
    canReceiveItem: (stepIndex: number, from: ItemLocation) => boolean
}

/** Moves the drag reports; the editor decides what to do with them. */
export interface StepDragHandlers {
    reorderStep: (from: number, insert: number) => void
    moveItem: (from: ItemLocation, to: ItemLocation) => void
    cancelled: () => void
}

export interface StepDrag {
    startStep: (index: number, event: DragEvent) => void
    startItem: (stepIndex: number, itemIndex: number, event: DragEvent) => void
    end: () => void
    drop: (event: DragEvent) => void
    overCard: (index: number, event: DragEvent) => void
    overItem: (stepIndex: number, itemIndex: number, event: DragEvent) => void
    isDraggedStep: (index: number) => boolean
    isDraggedItem: (stepIndex: number, itemIndex: number) => boolean
    isStepInsertAt: (at: number) => boolean
    isItemInsertAt: (stepIndex: number, at: number) => boolean
}

interface DragScope {
    drag: DragReorder<DragSource, DropTarget>
    source: StepDragSource
}

export function useStepDrag(source: StepDragSource, handlers: StepDragHandlers): StepDrag {
    const drag = useDragReorder<DragSource, DropTarget>({
        onCommit: (from, to) => commitDrop(handlers, from, to),
        onCancel: handlers.cancelled,
    })
    const scope: DragScope = { drag, source }

    return {
        startStep: (index, event) => drag.start({ kind: 'step', index }, event),
        startItem: (step, index, event) => drag.start({ kind: 'item', step, index }, event),
        end: drag.end,
        drop: drag.drop,
        overCard: (index, event) => cardDragOver(scope, index, event),
        overItem: (step, index, event) => itemDragOver(scope, { step, index }, event),
        isDraggedStep: (index) => isDraggedStep(drag, index),
        isDraggedItem: (step, index) => isDraggedItem(drag, { step, index }),
        isStepInsertAt: (at) => isStepInsertAt(drag, at),
        isItemInsertAt: (step, at) => isItemInsertAt(drag, step, at),
    }
}

function commitDrop(handlers: StepDragHandlers, source: DragSource, target: DropTarget): void {
    if (source.kind === 'step' && target.kind === 'step') {
        handlers.reorderStep(source.index, target.insert)
    } else if (source.kind === 'item' && target.kind === 'item') {
        handlers.moveItem(
            { step: source.step, index: source.index },
            { step: target.step, index: target.insert },
        )
    }
}

/** Whether the active item drag may drop into this step (capacity rules). */
function acceptsItemDrop(scope: DragScope, stepIndex: number): boolean {
    const source = scope.drag.source.value
    if (!source || source.kind === 'step') return false

    return scope.source.canReceiveItem(stepIndex, { step: source.step, index: source.index })
}

/** Card-level dragover: step drags reorder the card, item drags append to it. */
function cardDragOver(scope: DragScope, index: number, event: DragEvent): void {
    if (scope.drag.source.value?.kind === 'step') {
        scope.drag.over({ kind: 'step', insert: isPastMidpointY(event) ? index + 1 : index }, event)
    } else if (acceptsItemDrop(scope, index)) {
        const insert = scope.source.steps[index]?.items.length ?? 0
        scope.drag.over({ kind: 'item', step: index, insert }, event)
    }
}

/** Medallion-level dragover: precise insertion side from the pointer X. */
function itemDragOver(scope: DragScope, at: ItemLocation, event: DragEvent): void {
    if (scope.drag.source.value?.kind === 'step' || !acceptsItemDrop(scope, at.step)) return
    event.stopPropagation()
    const insert = isPastMidpointX(event) ? at.index + 1 : at.index
    scope.drag.over({ kind: 'item', step: at.step, insert }, event)
}

function isPastMidpointY(event: DragEvent): boolean {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()

    return event.clientY > rect.top + rect.height / 2
}

function isPastMidpointX(event: DragEvent): boolean {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()

    return event.clientX > rect.left + rect.width / 2
}

function isDraggedStep(drag: DragReorder<DragSource, DropTarget>, index: number): boolean {
    const source = drag.source.value

    return source?.kind === 'step' && source.index === index
}

function isDraggedItem(drag: DragReorder<DragSource, DropTarget>, at: ItemLocation): boolean {
    const source = drag.source.value

    return source?.kind === 'item' && source.step === at.step && source.index === at.index
}

function isStepInsertAt(drag: DragReorder<DragSource, DropTarget>, at: number): boolean {
    const target = drag.target.value

    return target?.kind === 'step' && target.insert === at
}

function isItemInsertAt(
    drag: DragReorder<DragSource, DropTarget>,
    stepIndex: number,
    at: number,
): boolean {
    const target = drag.target.value

    return target?.kind === 'item' && target.step === stepIndex && target.insert === at
}
