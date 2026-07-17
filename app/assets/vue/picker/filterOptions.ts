/**
 * Pure filtering helpers of the favorite picker — no Vue, no DOM, no fetch.
 */

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

const COMBINING_MARKS = /[\u0300-\u036f]/g

/** Lowercased, accent-stripped comparison form ("Séraphine" → "seraphine"). */
export function normalizeSearchText(value: string): string {
    return value.normalize('NFD').replace(COMBINING_MARKS, '').toLowerCase()
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

    const matches = new Set<PickerEntry>()
    const matchedGroupIds = new Set<string>()
    const groupsWithMatch = new Set<string>()
    for (const entry of entries) {
        if (!entry.searchText.includes(q)) {
            continue
        }
        matches.add(entry)
        if (entry.isGroup) {
            matchedGroupIds.add(entry.id)
        } else if (entry.groupId !== undefined) {
            groupsWithMatch.add(entry.groupId)
        }
    }

    return entries.filter((entry) => {
        if (matches.has(entry)) {
            return true
        }
        if (entry.isGroup) {
            return groupsWithMatch.has(entry.id)
        }
        return entry.groupId !== undefined && matchedGroupIds.has(entry.groupId)
    })
}
