import { describe, expect, it } from 'vitest'
import {
    addItem,
    addStep,
    buildGold,
    canAddItem,
    canAddStep,
    createStep,
    insertItem,
    MAX_ITEMS_PER_STEP,
    MAX_STEPS,
    MAX_TOTAL_ITEMS,
    moveItem,
    moveItemToIndex,
    moveStep,
    moveStepToIndex,
    removeItem,
    removeStep,
    stepGold,
    totalItems,
    transferItem,
    updateStep,
} from './stepList'
import type { BuildStep } from './structure'

function steps(...items: string[][]): BuildStep[] {
    return items.map((ids, i) => ({ label: `Step ${i + 1}`, note: null, items: ids }))
}

describe('step bounds', () => {
    it('adds up to MAX_STEPS then refuses', () => {
        let list: BuildStep[] = [createStep('Start')]
        while (canAddStep(list)) list = addStep(list)
        expect(list).toHaveLength(MAX_STEPS)
        expect(addStep(list)).toBe(list)
    })

    it('removes by index and ignores out-of-range', () => {
        const list = steps(['1055'], ['3006'])
        expect(removeStep(list, 0)).toHaveLength(1)
        expect(removeStep(list, 5)).toBe(list)
        expect(removeStep(list, -1)).toBe(list)
    })

    it('moves a step and clamps at the edges', () => {
        const list = steps(['a'], ['b'], ['c'])
        const moved = moveStep(list, 0, 1)
        expect(moved.map((s) => s.items[0])).toEqual(['b', 'a', 'c'])
        expect(moveStep(list, 0, -1)).toBe(list)
        expect(moveStep(list, 2, 1)).toBe(list)
    })

    it('patches label/note immutably', () => {
        const list = steps(['a'])
        const next = updateStep(list, 0, { label: 'Core', note: 'rush it' })
        expect(next[0]).toEqual({ label: 'Core', note: 'rush it', items: ['a'] })
        expect(list[0]?.label).toBe('Step 1')
        expect(updateStep(list, 9, { label: 'x' })).toBe(list)
    })
})

describe('item bounds', () => {
    it('caps items per step', () => {
        let list = steps([])
        for (let i = 0; i < MAX_ITEMS_PER_STEP; i++) list = addItem(list, 0, '2003')
        expect(list[0]?.items).toHaveLength(MAX_ITEMS_PER_STEP)
        expect(canAddItem(list, 0)).toBe(false)
        expect(addItem(list, 0, '2003')).toBe(list)
    })

    it('allows duplicate item ids (multiple purchases)', () => {
        let list = steps([])
        list = addItem(list, 0, '2003')
        list = addItem(list, 0, '2003')
        expect(list[0]?.items).toEqual(['2003', '2003'])
    })

    it('caps the build-wide total', () => {
        // 5 steps x 8 items = 40 = MAX_TOTAL_ITEMS.
        let list = steps([], [], [], [], [], [])
        for (let s = 0; s < 5; s++) {
            for (let i = 0; i < MAX_ITEMS_PER_STEP; i++) list = addItem(list, s, '1055')
        }
        expect(totalItems(list)).toBe(MAX_TOTAL_ITEMS)
        expect(canAddItem(list, 5)).toBe(false)
        expect(addItem(list, 5, '1055')).toBe(list)
    })

    it('removes and reorders items within a step', () => {
        const list = steps(['a', 'b', 'c'])
        expect(removeItem(list, 0, 1)[0]?.items).toEqual(['a', 'c'])
        expect(moveItem(list, 0, 2, -1)[0]?.items).toEqual(['a', 'c', 'b'])
        expect(moveItem(list, 0, 0, -1)).toBe(list)
        expect(moveItem(list, 1, 0, 1)).toBe(list) // unknown step
    })
})

describe('drop semantics (insertion indexes)', () => {
    it('moves a step to an insertion point, adjusting for its own removal', () => {
        const list = steps(['a'], ['b'], ['c'])
        expect(moveStepToIndex(list, 0, 3).map((s) => s.items[0])).toEqual(['b', 'c', 'a'])
        expect(moveStepToIndex(list, 2, 0).map((s) => s.items[0])).toEqual(['c', 'a', 'b'])
        // Dropping right before or right after itself changes nothing.
        expect(moveStepToIndex(list, 1, 1)).toBe(list)
        expect(moveStepToIndex(list, 1, 2)).toBe(list)
        expect(moveStepToIndex(list, 9, 0)).toBe(list)
    })

    it('reorders items inside a step to an insertion point', () => {
        const list = steps(['a', 'b', 'c'])
        expect(moveItemToIndex(list, 0, 0, 3)[0]?.items).toEqual(['b', 'c', 'a'])
        expect(moveItemToIndex(list, 0, 2, 0)[0]?.items).toEqual(['c', 'a', 'b'])
        expect(moveItemToIndex(list, 0, 1, 99)[0]?.items).toEqual(['a', 'c', 'b'])
        expect(moveItemToIndex(list, 0, 1, 1)).toBe(list)
        expect(moveItemToIndex(list, 5, 0, 1)).toBe(list)
    })

    it('inserts an item at an exact clamped position, capacity-checked', () => {
        const list = steps(['a', 'c'])
        expect(insertItem(list, 0, 1, 'b')[0]?.items).toEqual(['a', 'b', 'c'])
        expect(insertItem(list, 0, -5, 'z')[0]?.items).toEqual(['z', 'a', 'c'])
        expect(insertItem(list, 0, 99, 'z')[0]?.items).toEqual(['a', 'c', 'z'])

        let full = steps([])
        for (let i = 0; i < MAX_ITEMS_PER_STEP; i++) full = insertItem(full, 0, 0, 'x')
        expect(insertItem(full, 0, 0, 'x')).toBe(full)
    })

    it('transfers an item across steps at the target insertion point', () => {
        const list = steps(['a', 'b'], ['c'])
        const next = transferItem(list, { step: 0, index: 1 }, { step: 1, index: 0 })
        expect(next[0]?.items).toEqual(['a'])
        expect(next[1]?.items).toEqual(['b', 'c'])
    })

    it('same-step transfers degrade to a reorder', () => {
        const list = steps(['a', 'b', 'c'])
        expect(transferItem(list, { step: 0, index: 0 }, { step: 0, index: 3 })[0]?.items).toEqual(['b', 'c', 'a'])
    })

    it('refuses a transfer into a full step and nonsense locations', () => {
        const full = steps(Array.from({ length: MAX_ITEMS_PER_STEP }, () => 'x'), ['y'])
        expect(transferItem(full, { step: 1, index: 0 }, { step: 0, index: 0 })).toBe(full)
        expect(transferItem(full, { step: 5, index: 0 }, { step: 0, index: 0 })).toBe(full)
        expect(transferItem(full, { step: 1, index: 9 }, { step: 0, index: 0 })).toBe(full)
    })

    it('may empty the source step (the server enforces the 1-item minimum at submit)', () => {
        const list = steps(['a'], ['b'])
        const next = transferItem(list, { step: 0, index: 0 }, { step: 1, index: 1 })
        expect(next[0]?.items).toEqual([])
        expect(next[1]?.items).toEqual(['b', 'a'])
    })
})

describe('gold totals', () => {
    const price: Record<string, number> = { '1055': 450, '3006': 1100 }
    const goldOf = (id: string): number | null => price[id] ?? null

    it('sums a step, counting unknown ids (ghosts) as zero', () => {
        expect(stepGold({ label: '', note: null, items: ['1055', '3006', 'gone'] }, goldOf)).toBe(1550)
    })

    it('sums the whole build', () => {
        expect(buildGold(steps(['1055'], ['3006', '3006']), goldOf)).toBe(2650)
    })
})
