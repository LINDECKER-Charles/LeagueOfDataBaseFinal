import { ref, watch, type Ref } from 'vue'
import { secondarySlotIndex, type RuneTree } from '../catalog/catalogTypes'
import {
    draftFromRunes,
    emptyRuneDraft,
    GHOST_SLOT,
    selectPrimaryPerk,
    selectPrimaryStyle,
    selectSecondaryPerk,
    selectSecondaryStyle,
    type RuneDraft,
} from './runeRules'
import type { BuildRunes } from '../editor/structure'

export interface RuneDraftControls {
    runeDraft: Ref<RuneDraft>
    setPrimaryStyle: (styleId: number) => void
    setPrimaryPerk: (slot: number, perkId: number) => void
    setSecondaryStyle: (styleId: number) => void
    setSecondaryPerk: (slot: number, perkId: number) => void
}

/**
 * Rune selection of the editor, wiring the pure {@link runeRules} to reactive
 * state. Secondary slot indexes need the runes catalog: until it lands the
 * initial picks carry GHOST_SLOT and are re-anchored by the watcher below.
 */
export function useRuneDraft(
    initial: BuildRunes | null,
    trees: Ref<RuneTree[] | null>,
): RuneDraftControls {
    const runeDraft = ref<RuneDraft>(
        initial ? draftFromRunes(initial, () => null) : emptyRuneDraft(),
    )
    const apply = (next: RuneDraft): void => void (runeDraft.value = next)

    watch(trees, (loaded) => {
        if (loaded) apply(reanchorSecondary(runeDraft.value, loaded))
    })

    return {
        runeDraft,
        setPrimaryStyle: (styleId) => apply(selectPrimaryStyle(runeDraft.value, styleId)),
        setPrimaryPerk: (slot, perkId) => apply(selectPrimaryPerk(runeDraft.value, slot, perkId)),
        setSecondaryStyle: (styleId) => apply(selectSecondaryStyle(runeDraft.value, styleId)),
        setSecondaryPerk: (slot, perkId) =>
            apply(selectSecondaryPerk(runeDraft.value, slot, perkId)),
    }
}

/** Resolve GHOST_SLOT picks against the freshly loaded catalog (true ghosts stay). */
function reanchorSecondary(draft: RuneDraft, trees: RuneTree[]): RuneDraft {
    const styleId = draft.secondaryStyleId
    if (styleId === null || draft.secondaryPicks.every((p) => p.slotIndex !== GHOST_SLOT)) {
        return draft
    }

    return {
        ...draft,
        secondaryPicks: draft.secondaryPicks.map((pick) => ({
            ...pick,
            slotIndex: pick.slotIndex === GHOST_SLOT
                ? (secondarySlotIndex(trees, styleId, pick.perkId) ?? GHOST_SLOT)
                : pick.slotIndex,
        })),
    }
}
