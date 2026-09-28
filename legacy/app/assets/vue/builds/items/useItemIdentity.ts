import { computed, type Ref } from 'vue'
import type { ItemOption } from '../catalog/catalogTypes'

/** Why a placed item renders ghosted; null when it is fine (or unknown yet). */
export type GhostReason = 'patch' | 'mode' | null

export interface ItemIdentity {
    /** Display identity of a placed item: current catalog first, then any seen. */
    resolveItem: (itemId: string) => ItemOption | undefined
    /** Ghost verdict once the ACTIVE catalog answered; never judges while loading. */
    ghostOf: (itemId: string) => GhostReason
}

/**
 * Identity of a placed item across context switches. `known` accumulates every
 * option seen this session, so a medallion keeps its name/icon/gold after the
 * (patch, mode) context that knew it is switched away — and the editor can tell
 * "unknown on this patch" apart from "excluded by this game mode".
 */
export function useItemIdentity(
    options: Ref<ItemOption[] | null>,
    known: Ref<Record<string, ItemOption>>,
): ItemIdentity {
    const byId = computed<Map<string, ItemOption>>(() => {
        const index = new Map<string, ItemOption>()
        for (const option of options.value ?? []) index.set(option.id, option)
        return index
    })

    return {
        resolveItem: (itemId) => byId.value.get(itemId) ?? known.value[itemId],
        ghostOf: (itemId) => {
            if (options.value === null || byId.value.has(itemId)) return null
            return known.value[itemId] ? 'mode' : 'patch'
        },
    }
}
