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
    if (!canAddItem(steps, stepIndex)) return steps
    return steps.map((step, i) => (i === stepIndex ? { ...step, items: [...step.items, itemId] } : step))
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
