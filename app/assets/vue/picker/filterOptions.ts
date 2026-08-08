/**
 * Pure filtering helpers of the favorite picker — no Vue, no DOM, no fetch.
 */

import { normalizeSearchText } from '../search/normalizeSearchText'

/** One selectable row of the picker list (rune trees double as group headers). */
export interface PickerEntry {
    id: string
    name: string
    image: string | null
    /** Precomputed normalized haystack ({@link normalizeSearchText}). */
    searchText: string
    /** Tree id this perk belongs to — drives indentation and group matching. */
    groupId?: string
    /** Rune-tree header row (selectable too: a favorite can be a whole tree). */
    isGroup?: boolean
}

/** What matched a query, and which groups that pulls in either way. */
interface MatchIndex {
    entries: Set<PickerEntry>
    /** Trees whose own header matched: all of their perks stay visible. */
    matchedGroupIds: Set<string>
    /** Trees with at least one matching perk: their header stays visible. */
    groupsWithMatch: Set<string>
}

/**
 * Case/accent-insensitive filter preserving input order (stable — no resort).
 * Group awareness keeps the rune list readable: a header stays when any of its
 * perks matches, and a matching header keeps all of its perks visible.
 */
export function filterOptions(entries: readonly PickerEntry[], query: string): PickerEntry[] {
    const q = normalizeSearchText(query.trim())
    if (q === '') {
        return [...entries]
    }
    const index = indexMatches(entries, q)

    return entries.filter((entry) => isVisible(entry, index))
}

function indexMatches(entries: readonly PickerEntry[], q: string): MatchIndex {
    const index: MatchIndex = {
        entries: new Set(),
        matchedGroupIds: new Set(),
        groupsWithMatch: new Set(),
    }
    for (const entry of entries) {
        if (!entry.searchText.includes(q)) {
            continue
        }
        index.entries.add(entry)
        if (entry.isGroup) {
            index.matchedGroupIds.add(entry.id)
        } else if (entry.groupId !== undefined) {
            index.groupsWithMatch.add(entry.groupId)
        }
    }

    return index
}

function isVisible(entry: PickerEntry, index: MatchIndex): boolean {
    if (index.entries.has(entry)) {
        return true
    }
    if (entry.isGroup) {
        return index.groupsWithMatch.has(entry.id)
    }

    return entry.groupId !== undefined && index.matchedGroupIds.has(entry.groupId)
}
