import { describe, expect, it } from 'vitest'
import {
    addItem,
    addStep,
    buildGold,
    canAddItem,
    canAddStep,
    createStep,
    MAX_ITEMS_PER_STEP,
    MAX_STEPS,
    MAX_TOTAL_ITEMS,
    moveItem,
    moveStep,
    removeItem,
    removeStep,
    stepGold,
    totalItems,
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
