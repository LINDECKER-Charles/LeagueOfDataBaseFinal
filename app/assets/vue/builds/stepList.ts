/**
 * Pure purchase-order (steps) rules of the build editor. Mirrors the server
 * bounds (BuildStructureValidator): 1..10 steps, 1..8 items per step, 40 items
 * total, duplicates allowed (potions, stacked components). Every function
 * returns a NEW array; out-of-bounds requests are no-ops, never throws.
 */

import type { BuildStep } from './structure'

export const MAX_STEPS = 10
export const MIN_STEPS = 1
export const MAX_ITEMS_PER_STEP = 8
export const MAX_TOTAL_ITEMS = 40
export const MAX_LABEL_LENGTH = 40
export const MAX_NOTE_LENGTH = 300

export function createStep(label = ''): BuildStep {
    return { label, note: null, items: [] }
}

export function totalItems(steps: BuildStep[]): number {
    return steps.reduce((sum, step) => sum + step.items.length, 0)
}

export function canAddStep(steps: BuildStep[]): boolean {
    return steps.length < MAX_STEPS
}

export function addStep(steps: BuildStep[], label = ''): BuildStep[] {
    return canAddStep(steps) ? [...steps, createStep(label)] : steps
}

export function removeStep(steps: BuildStep[], index: number): BuildStep[] {
    if (index < 0 || index >= steps.length) return steps
    return steps.filter((_, i) => i !== index)
}

export function moveStep(steps: BuildStep[], index: number, delta: number): BuildStep[] {
    return moveEntry(steps, index, delta)
}

export function updateStep(
    steps: BuildStep[],
    index: number,
    patch: Partial<Pick<BuildStep, 'label' | 'note'>>,
): BuildStep[] {
    const step = steps[index]
    if (!step) return steps
    return steps.map((s, i) => (i === index ? { ...s, ...patch } : s))
}

export function canAddItem(steps: BuildStep[], stepIndex: number): boolean {
    const step = steps[stepIndex]
    return !!step && step.items.length < MAX_ITEMS_PER_STEP && totalItems(steps) < MAX_TOTAL_ITEMS
}

export function addItem(steps: BuildStep[], stepIndex: number, itemId: string): BuildStep[] {
    return insertItem(steps, stepIndex, steps[stepIndex]?.items.length ?? 0, itemId)
}

/** Insert at an exact position (drop semantics), clamped; capacity rules of addItem apply. */
export function insertItem(steps: BuildStep[], stepIndex: number, index: number, itemId: string): BuildStep[] {
    if (!canAddItem(steps, stepIndex)) return steps
    return steps.map((step, i) => {
        if (i !== stepIndex) return step
        const at = clampInsert(index, step.items.length)
        return { ...step, items: [...step.items.slice(0, at), itemId, ...step.items.slice(at)] }
    })
}

export function removeItem(steps: BuildStep[], stepIndex: number, itemIndex: number): BuildStep[] {
    const step = steps[stepIndex]
    if (!step || itemIndex < 0 || itemIndex >= step.items.length) return steps
    return steps.map((s, i) => (i === stepIndex ? { ...s, items: s.items.filter((_, j) => j !== itemIndex) } : s))
}

export function moveItem(steps: BuildStep[], stepIndex: number, itemIndex: number, delta: number): BuildStep[] {
    const step = steps[stepIndex]
    if (!step) return steps
    const items = moveEntry(step.items, itemIndex, delta)
    return items === step.items ? steps : steps.map((s, i) => (i === stepIndex ? { ...s, items } : s))
}

/** Move a step to an INSERTION index (0..length, drop semantics); identity when nothing changes. */
export function moveStepToIndex(steps: BuildStep[], fromIndex: number, insertIndex: number): BuildStep[] {
    return moveEntryToIndex(steps, fromIndex, insertIndex)
}

/** Reorder one step's items to an INSERTION index (0..length); identity when nothing changes. */
export function moveItemToIndex(steps: BuildStep[], stepIndex: number, fromIndex: number, insertIndex: number): BuildStep[] {
    const step = steps[stepIndex]
    if (!step) return steps
    const items = moveEntryToIndex(step.items, fromIndex, insertIndex)
    return items === step.items ? steps : steps.map((s, i) => (i === stepIndex ? { ...s, items } : s))
}

/** A slot in the purchase order: step index + item position inside it. */
export interface ItemLocation {
    step: number
    index: number
}

/**
 * Move an item across steps (to.index is an insertion index). The build total
 * is unchanged, so only the target step's per-step cap applies. Same-step
 * transfers degrade to a plain reorder.
 */
export function transferItem(steps: BuildStep[], from: ItemLocation, to: ItemLocation): BuildStep[] {
    if (from.step === to.step) return moveItemToIndex(steps, from.step, from.index, to.index)
    const source = steps[from.step]
    const target = steps[to.step]
    const itemId = source?.items[from.index]
    if (!source || !target || itemId === undefined || target.items.length >= MAX_ITEMS_PER_STEP) return steps

    const at = clampInsert(to.index, target.items.length)
    return steps.map((s, i) => {
        if (i === from.step) return { ...s, items: s.items.filter((_, j) => j !== from.index) }
        if (i === to.step) return { ...s, items: [...s.items.slice(0, at), itemId, ...s.items.slice(at)] }
        return s
    })
}

/** Sum of the known item costs of one step; unknown ids (ghosts) count 0. */
export function stepGold(step: BuildStep, goldOf: (itemId: string) => number | null): number {
    return step.items.reduce((sum, id) => sum + (goldOf(id) ?? 0), 0)
}

export function buildGold(steps: BuildStep[], goldOf: (itemId: string) => number | null): number {
    return steps.reduce((sum, step) => sum + stepGold(step, goldOf), 0)
}

/** Swap-with-neighbour move shared by steps and items; identity when impossible. */
function moveEntry<T>(entries: T[], index: number, delta: number): T[] {
    const target = index + delta
    if (index < 0 || index >= entries.length || target < 0 || target >= entries.length || delta === 0) {
        return entries
    }
    const next = [...entries]
    const [entry] = next.splice(index, 1)
    next.splice(target, 0, entry as T)
    return next
}

/**
 * Move to an insertion index shared by steps and items. Removing the dragged
 * entry shifts insertion points past it down by one — the classic drop fix-up.
 */
function moveEntryToIndex<T>(entries: T[], from: number, insertIndex: number): T[] {
    if (from < 0 || from >= entries.length) return entries
    const target = clampInsert(insertIndex, entries.length)
    const resting = target > from ? target - 1 : target
    if (resting === from) return entries
    const next = [...entries]
    const [entry] = next.splice(from, 1)
    next.splice(resting, 0, entry as T)
    return next
}

function clampInsert(index: number, length: number): number {
    return Math.max(0, Math.min(length, index))
}
