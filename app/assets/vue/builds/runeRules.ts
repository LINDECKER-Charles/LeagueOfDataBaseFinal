/**
 * Pure rune-page rules of the build editor — the exact LoL-client behaviour:
 * one perk per primary slot, and TWO secondary perks that must come from two
 * DIFFERENT minor slots. Re-picking an occupied secondary slot REPLACES that
 * slot's perk; picking a third distinct slot evicts the OLDEST pick (FIFO).
 * All functions return new objects (no mutation) so Vue reactivity stays cheap.
 */

import type { BuildRunes } from './structure'

export const PRIMARY_SLOTS = 4
export const SECONDARY_PICKS = 2
export const KEYSTONE_SLOT = 0
/** Slot index of a stored pick whose perk no longer exists in the catalog. */
export const GHOST_SLOT = -1

export interface SecondaryPick {
    slotIndex: number
    perkId: number
}

export interface RuneDraft {
    primaryStyleId: number | null
    /** One perk per primary slot (index = slot, slot 0 = keystone). */
    primaryPerks: (number | null)[]
    secondaryStyleId: number | null
    /** Ordered oldest -> newest, at most {@link SECONDARY_PICKS}. */
    secondaryPicks: SecondaryPick[]
}

export function emptyRuneDraft(): RuneDraft {
    return {
        primaryStyleId: null,
        primaryPerks: Array.from({ length: PRIMARY_SLOTS }, () => null),
        secondaryStyleId: null,
        secondaryPicks: [],
    }
}

/** Switching primary tree resets its perks; a colliding secondary tree is cleared. */
export function selectPrimaryStyle(draft: RuneDraft, styleId: number): RuneDraft {
    if (draft.primaryStyleId === styleId) return draft
    const collides = draft.secondaryStyleId === styleId
    return {
        primaryStyleId: styleId,
        primaryPerks: Array.from({ length: PRIMARY_SLOTS }, () => null),
        secondaryStyleId: collides ? null : draft.secondaryStyleId,
        secondaryPicks: collides ? [] : draft.secondaryPicks,
    }
}

export function selectPrimaryPerk(draft: RuneDraft, slotIndex: number, perkId: number): RuneDraft {
    if (slotIndex < 0 || slotIndex >= PRIMARY_SLOTS) return draft
    const primaryPerks = [...draft.primaryPerks]
    primaryPerks[slotIndex] = perkId
    return { ...draft, primaryPerks }
}

/** The primary tree can never double as secondary; switching resets the picks. */
export function selectSecondaryStyle(draft: RuneDraft, styleId: number): RuneDraft {
    if (styleId === draft.primaryStyleId || styleId === draft.secondaryStyleId) return draft
    return { ...draft, secondaryStyleId: styleId, secondaryPicks: [] }
}

/**
 * Secondary pick with the client's replacement/eviction rule:
 *  - keystone slot is unreachable;
 *  - a perk of an already-used slot replaces that slot's pick (and becomes newest);
 *  - a third distinct slot evicts the oldest pick.
 */
export function selectSecondaryPerk(draft: RuneDraft, slotIndex: number, perkId: number): RuneDraft {
    if (slotIndex === KEYSTONE_SLOT) return draft
    const secondaryPicks = draft.secondaryPicks.filter((pick) => pick.slotIndex !== slotIndex)
    secondaryPicks.push({ slotIndex, perkId })
    while (secondaryPicks.length > SECONDARY_PICKS) secondaryPicks.shift()
    return { ...draft, secondaryPicks }
}

export function isRuneDraftComplete(draft: RuneDraft): boolean {
    return (
        draft.primaryStyleId !== null &&
        draft.secondaryStyleId !== null &&
        draft.primaryPerks.every((perk) => perk !== null) &&
        draft.secondaryPicks.length === SECONDARY_PICKS
    )
}

/**
 * Serializable selection — emitted even while incomplete (nulls dropped) so the
 * hidden input always carries the user's real progress; the server rejects
 * incomplete payloads with its own error codes.
 */
export function draftRunes(draft: RuneDraft): BuildRunes {
    return {
        primaryStyleId: draft.primaryStyleId ?? 0,
        primarySelections: draft.primaryPerks.filter((perk): perk is number => perk !== null),
        secondaryStyleId: draft.secondaryStyleId ?? 0,
        secondarySelections: draft.secondaryPicks.map((pick) => pick.perkId),
    }
}

/**
 * Rebuild an editing draft from a stored selection. `slotOfSecondary` maps a
 * (styleId, perkId) to its minor-slot index in the current catalog; unknown
 * perks keep a {@link GHOST_SLOT} so they stay VISIBLE (the user decides) while
 * never colliding with a real slot in the replacement rule.
 */
export function draftFromRunes(
    runes: BuildRunes,
    slotOfSecondary: (styleId: number, perkId: number) => number | null,
): RuneDraft {
    const primaryPerks = Array.from(
        { length: PRIMARY_SLOTS },
        (_, slot) => runes.primarySelections[slot] ?? null,
    )
    const secondaryPicks = runes.secondarySelections.slice(0, SECONDARY_PICKS).map((perkId) => ({
        slotIndex: slotOfSecondary(runes.secondaryStyleId, perkId) ?? GHOST_SLOT,
        perkId,
    }))
    return {
        primaryStyleId: runes.primaryStyleId || null,
        primaryPerks,
        secondaryStyleId: runes.secondaryStyleId || null,
        secondaryPicks,
    }
}
