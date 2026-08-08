import { computed, ref, type ComputedRef, type Ref } from 'vue'
import { formatTemplate } from '../../i18n/formatTemplate'
import type { DndLabels } from '../editor/editorLabels'
import {
    addItem,
    addStep,
    canAddItem,
    canAddStep,
    createStep,
    MAX_ITEMS_PER_STEP,
    moveItem,
    moveItemToIndex,
    moveStep,
    moveStepToIndex,
    removeItem,
    removeStep,
    restingIndex,
    transferItem,
    updateStep,
    type ItemLocation,
} from './stepList'
import type { BuildStep } from '../editor/structure'

type StepPatch = Partial<Pick<BuildStep, 'label' | 'note'>>

/** Shared context of the step/item commands: the list, how to commit, how to say it. */
interface StepEditingContext {
    steps: Ref<BuildStep[]>
    commit: (next: BuildStep[], message: string, params: Record<string, number>) => void
    dnd: DndLabels
}

export interface StepEditing {
    steps: Ref<BuildStep[]>
    canAddStep: ComputedRef<boolean>
    announceDragCancelled: () => void
    appendStep: (label?: string) => void
    deleteStep: (index: number) => void
    editStep: (index: number, patch: StepPatch) => void
    shiftStep: (index: number, delta: number) => void
    dropStep: (from: number, insert: number) => void
    appendItem: (stepIndex: number, itemId: string) => void
    deleteItem: (stepIndex: number, itemIndex: number) => void
    shiftItem: (stepIndex: number, itemIndex: number, delta: number) => void
    dropItem: (from: ItemLocation, to: ItemLocation) => void
    canAddItemTo: (stepIndex: number) => boolean
    canReceiveItem: (stepIndex: number, from: ItemLocation) => boolean
}

/**
 * Purchase order of the editor: every move (buttons and drag-and-drop) funnels
 * through the pure {@link stepList} helpers and announces politely. Identity
 * results (a move that changes nothing) stay silent.
 */
export function useStepEditing(
    initial: BuildStep[],
    announce: (message: string) => void,
    dnd: DndLabels,
): StepEditing {
    const steps = ref<BuildStep[]>(initial.length > 0 ? initial : [createStep()])

    const commit = (next: BuildStep[], message: string, params: Record<string, number>): void => {
        if (next === steps.value) return
        steps.value = next
        announce(formatTemplate(message, params))
    }
    const context: StepEditingContext = { steps, commit, dnd }

    return {
        steps,
        canAddStep: computed(() => canAddStep(steps.value)),
        announceDragCancelled: () => announce(dnd.cancelled),
        ...stepCommands(context),
        ...itemCommands(context),
    }
}

/** Step-level edits: add, remove, retitle, reorder. */
function stepCommands({ steps, commit, dnd }: StepEditingContext) {
    return {
        appendStep: (label = '') => void (steps.value = addStep(steps.value, label)),
        deleteStep: (index: number) => void (steps.value = removeStep(steps.value, index)),
        editStep: (index: number, patch: StepPatch) =>
            void (steps.value = updateStep(steps.value, index, patch)),
        shiftStep: (index: number, delta: number) =>
            commit(moveStep(steps.value, index, delta), dnd.movedStep, {
                position: index + delta + 1,
            }),
        dropStep: (from: number, insert: number) =>
            commit(moveStepToIndex(steps.value, from, insert), dnd.movedStep, {
                position: restingIndex(from, insert, steps.value.length) + 1,
            }),
    }
}

/** Item-level edits inside and across steps, plus their capacity guards. */
function itemCommands({ steps, commit, dnd }: StepEditingContext) {
    /** Landing position of a same-step item drop, against that step's own length. */
    const sameStepResting = (from: ItemLocation, insert: number): number =>
        restingIndex(from.index, insert, steps.value[from.step]?.items.length ?? 0)

    return {
        appendItem: (stepIndex: number, itemId: string) =>
            commit(addItem(steps.value, stepIndex, itemId), dnd.added, { step: stepIndex + 1 }),
        deleteItem: (stepIndex: number, itemIndex: number) =>
            void (steps.value = removeItem(steps.value, stepIndex, itemIndex)),
        shiftItem: (stepIndex: number, itemIndex: number, delta: number) =>
            commit(moveItem(steps.value, { step: stepIndex, index: itemIndex }, delta),
                dnd.movedItem, { position: itemIndex + delta + 1 }),
        dropItem: (from: ItemLocation, to: ItemLocation) => (from.step === to.step
            ? commit(moveItemToIndex(steps.value, from, to.index), dnd.movedItem,
                { position: sameStepResting(from, to.index) + 1 })
            : commit(transferItem(steps.value, from, to), dnd.transferred, { step: to.step + 1 })),
        canAddItemTo: (stepIndex: number) => canAddItem(steps.value, stepIndex),
        // A same-step move never changes counts; a cross-step one only fights
        // the target's per-step cap (the build total is untouched).
        canReceiveItem: (stepIndex: number, from: ItemLocation): boolean =>
            from.step === stepIndex
            || (steps.value[stepIndex]?.items.length ?? MAX_ITEMS_PER_STEP) < MAX_ITEMS_PER_STEP,
    }
}
