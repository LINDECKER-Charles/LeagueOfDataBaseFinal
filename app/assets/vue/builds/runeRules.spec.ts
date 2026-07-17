import { describe, expect, it } from 'vitest'
import {
    draftFromRunes,
    draftRunes,
    emptyRuneDraft,
    GHOST_SLOT,
    isRuneDraftComplete,
    KEYSTONE_SLOT,
    selectPrimaryPerk,
    selectPrimaryStyle,
    selectSecondaryPerk,
    selectSecondaryStyle,
    type RuneDraft,
} from './runeRules'

const PRECISION = 8000
const DOMINATION = 8100
const SORCERY = 8200

function fullDraft(): RuneDraft {
    let draft = emptyRuneDraft()
    draft = selectPrimaryStyle(draft, PRECISION)
    draft = selectPrimaryPerk(draft, 0, 8005)
    draft = selectPrimaryPerk(draft, 1, 9101)
    draft = selectPrimaryPerk(draft, 2, 9104)
    draft = selectPrimaryPerk(draft, 3, 8014)
    draft = selectSecondaryStyle(draft, DOMINATION)
    draft = selectSecondaryPerk(draft, 1, 8126)
    draft = selectSecondaryPerk(draft, 2, 8138)
    return draft
}

describe('primary selection', () => {
    it('starts empty', () => {
        const draft = emptyRuneDraft()
        expect(draft.primaryStyleId).toBeNull()
        expect(draft.primaryPerks).toEqual([null, null, null, null])
        expect(draft.secondaryPicks).toEqual([])
    })

    it('sets one perk per slot', () => {
        let draft = selectPrimaryStyle(emptyRuneDraft(), PRECISION)
        draft = selectPrimaryPerk(draft, 1, 9101)
        draft = selectPrimaryPerk(draft, 1, 9111)
        expect(draft.primaryPerks).toEqual([null, 9111, null, null])
    })

    it('ignores out-of-range slots', () => {
        const draft = selectPrimaryStyle(emptyRuneDraft(), PRECISION)
        expect(selectPrimaryPerk(draft, 4, 1)).toBe(draft)
        expect(selectPrimaryPerk(draft, -1, 1)).toBe(draft)
    })

    it('switching primary tree resets its perks', () => {
        const next = selectPrimaryStyle(fullDraft(), SORCERY)
        expect(next.primaryPerks).toEqual([null, null, null, null])
        expect(next.secondaryStyleId).toBe(DOMINATION) // untouched, no collision
    })

    it('re-selecting the same primary tree keeps everything', () => {
        const draft = fullDraft()
        expect(selectPrimaryStyle(draft, PRECISION)).toBe(draft)
    })

    it('taking the secondary tree as primary clears the secondary side', () => {
        const next = selectPrimaryStyle(fullDraft(), DOMINATION)
        expect(next.secondaryStyleId).toBeNull()
        expect(next.secondaryPicks).toEqual([])
    })
})

describe('secondary selection — the LoL-client eviction rule', () => {
    it('rejects the primary tree as secondary', () => {
        const draft = selectPrimaryStyle(emptyRuneDraft(), PRECISION)
        expect(selectSecondaryStyle(draft, PRECISION)).toBe(draft)
    })

    it('switching secondary tree resets its picks', () => {
        const next = selectSecondaryStyle(fullDraft(), SORCERY)
        expect(next.secondaryStyleId).toBe(SORCERY)
        expect(next.secondaryPicks).toEqual([])
    })

    it('never accepts a keystone-slot pick', () => {
        const draft = fullDraft()
        expect(selectSecondaryPerk(draft, KEYSTONE_SLOT, 8112)).toBe(draft)
    })

    it('replaces the pick of an already-used slot', () => {
        let draft = selectSecondaryStyle(selectPrimaryStyle(emptyRuneDraft(), PRECISION), DOMINATION)
        draft = selectSecondaryPerk(draft, 1, 8126)
        draft = selectSecondaryPerk(draft, 1, 8139)
        expect(draft.secondaryPicks).toEqual([{ slotIndex: 1, perkId: 8139 }])
    })

    it('evicts the OLDEST pick when a third distinct slot is chosen', () => {
        let draft = selectSecondaryStyle(selectPrimaryStyle(emptyRuneDraft(), PRECISION), DOMINATION)
        draft = selectSecondaryPerk(draft, 1, 8126)
        draft = selectSecondaryPerk(draft, 2, 8138)
        draft = selectSecondaryPerk(draft, 3, 8106)
        expect(draft.secondaryPicks).toEqual([
            { slotIndex: 2, perkId: 8138 },
            { slotIndex: 3, perkId: 8106 },
        ])
    })

    it('same-slot replacement refreshes recency before eviction', () => {
        let draft = selectSecondaryStyle(selectPrimaryStyle(emptyRuneDraft(), PRECISION), DOMINATION)
        draft = selectSecondaryPerk(draft, 1, 8126) // oldest
        draft = selectSecondaryPerk(draft, 2, 8138)
        draft = selectSecondaryPerk(draft, 1, 8139) // replaces slot 1, now newest
        draft = selectSecondaryPerk(draft, 3, 8106) // evicts slot 2 (now oldest)
        expect(draft.secondaryPicks).toEqual([
            { slotIndex: 1, perkId: 8139 },
            { slotIndex: 3, perkId: 8106 },
        ])
    })
})

describe('serialization', () => {
    it('reports completeness', () => {
        expect(isRuneDraftComplete(emptyRuneDraft())).toBe(false)
        expect(isRuneDraftComplete(fullDraft())).toBe(true)
    })

    it('serializes a complete draft to the entity shape', () => {
        expect(draftRunes(fullDraft())).toEqual({
            primaryStyleId: PRECISION,
            primarySelections: [8005, 9101, 9104, 8014],
            secondaryStyleId: DOMINATION,
            secondarySelections: [8126, 8138],
        })
    })

    it('serializes partial progress without inventing values', () => {
        let draft = selectPrimaryStyle(emptyRuneDraft(), PRECISION)
        draft = selectPrimaryPerk(draft, 2, 9104)
        expect(draftRunes(draft)).toEqual({
            primaryStyleId: PRECISION,
            primarySelections: [9104],
            secondaryStyleId: 0,
            secondarySelections: [],
        })
    })
})

describe('draftFromRunes (edit mode)', () => {
    const stored = {
        primaryStyleId: PRECISION,
        primarySelections: [8005, 9101, 9104, 8014],
        secondaryStyleId: DOMINATION,
        secondarySelections: [8126, 8138],
    }

    it('re-anchors secondary picks through the slot resolver', () => {
        const draft = draftFromRunes(stored, (_styleId, perkId) => (perkId === 8126 ? 1 : perkId === 8138 ? 3 : null))
        expect(draft.primaryPerks).toEqual([8005, 9101, 9104, 8014])
        expect(draft.secondaryPicks).toEqual([
            { slotIndex: 1, perkId: 8126 },
            { slotIndex: 3, perkId: 8138 },
        ])
        expect(draft.secondaryStyleId).toBe(DOMINATION)
    })

    it('keeps unknown perks visible as ghost-slot picks', () => {
        const draft = draftFromRunes(stored, () => null)
        expect(draft.secondaryPicks).toEqual([
            { slotIndex: GHOST_SLOT, perkId: 8126 },
            { slotIndex: GHOST_SLOT, perkId: 8138 },
        ])
    })

    it('treats zeroed style ids as unselected', () => {
        const draft = draftFromRunes(
            { primaryStyleId: 0, primarySelections: [], secondaryStyleId: 0, secondarySelections: [] },
            () => null,
        )
        expect(draft.primaryStyleId).toBeNull()
        expect(draft.secondaryStyleId).toBeNull()
        expect(draft.primaryPerks).toEqual([null, null, null, null])
    })
})
