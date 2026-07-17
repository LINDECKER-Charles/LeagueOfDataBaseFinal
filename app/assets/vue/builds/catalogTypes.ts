/**
 * Typed views over the /api/picker/* payloads (contract owned by the picker
 * API) plus tolerant extractors: a malformed payload yields an empty catalog,
 * never a crash — the editor then shows its error/retry state.
 */

export interface ChampionOption {
    id: string
    key: string
    name: string
    image: string | null
}

export interface ItemOption {
    id: string
    name: string
    image: string | null
    gold: number
    purchasable: boolean
    tags: string[]
}

export interface RunePerk {
    id: number
    key: string
    name: string
    icon: string | null
    shortDesc: string
}

export interface RuneTree {
    id: number
    key: string
    name: string
    icon: string | null
    /** 4 slot arrays, slot 0 = keystones (picker contract). */
    slots: RunePerk[][]
}

function asRecord(value: unknown): Record<string, unknown> {
    return value !== null && typeof value === 'object' ? (value as Record<string, unknown>) : {}
}

function asArray(value: unknown): unknown[] {
    return Array.isArray(value) ? value : []
}

/**
 * Image/icon path ready for `src`: the picker serves root-relative "/cdn/…"
 * paths, but tolerate manager-style "cdn/…" (no leading slash) too.
 */
function webPath(value: unknown): string | null {
    if (typeof value !== 'string' || value === '') return null
    return value.startsWith('/') || value.startsWith('http') ? value : `/${value}`
}

export function championOptions(payload: unknown): ChampionOption[] {
    return asArray(asRecord(payload).options).map((raw) => {
        const o = asRecord(raw)
        return {
            id: String(o.id ?? ''),
            key: String(o.key ?? ''),
            name: String(o.name ?? o.id ?? ''),
            image: webPath(o.image),
        }
    })
}

export function itemOptions(payload: unknown): ItemOption[] {
    return asArray(asRecord(payload).options).map((raw) => {
        const o = asRecord(raw)
        return {
            id: String(o.id ?? ''),
            name: String(o.name ?? o.id ?? ''),
            image: webPath(o.image),
            gold: typeof o.gold === 'number' ? o.gold : 0,
            purchasable: o.purchasable !== false,
            tags: asArray(o.tags).map(String),
        }
    })
}

export function runeTrees(payload: unknown): RuneTree[] {
    return asArray(asRecord(payload).trees).map((raw) => {
        const t = asRecord(raw)
        return {
            id: Number(t.id ?? 0),
            key: String(t.key ?? ''),
            name: String(t.name ?? t.key ?? ''),
            icon: webPath(t.icon),
            slots: asArray(t.slots).map((slot) =>
                asArray(slot).map((perkRaw) => {
                    const p = asRecord(perkRaw)
                    return {
                        id: Number(p.id ?? 0),
                        key: String(p.key ?? ''),
                        name: String(p.name ?? p.key ?? ''),
                        icon: webPath(p.icon),
                        shortDesc: String(p.shortDesc ?? ''),
                    }
                }),
            ),
        }
    })
}

/** Minor-slot index (>= 1) of a perk in a tree; null when absent or keystone-only. */
export function secondarySlotIndex(trees: RuneTree[], styleId: number, perkId: number): number | null {
    const tree = trees.find((t) => t.id === styleId)
    if (!tree) return null
    for (let slot = 1; slot < tree.slots.length; slot++) {
        if ((tree.slots[slot] ?? []).some((perk) => perk.id === perkId)) return slot
    }
    return null
}
