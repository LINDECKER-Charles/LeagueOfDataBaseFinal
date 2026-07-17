import { reactive } from 'vue'
import { normalizeSearchText, type PickerEntry } from './filterOptions'

export type SlotType = 'champion' | 'item' | 'rune' | 'summoner'

export type CatalogStatus = 'idle' | 'loading' | 'ready' | 'error'

export interface CatalogState {
    status: CatalogStatus
    entries: PickerEntry[]
}

export interface PickerCatalogConfig {
    endpoints: Record<SlotType, string>
    version: string
    lang: string
}

export interface PickerCatalog {
    states: Record<SlotType, CatalogState>
    /** Fetch a type's catalogue once; safe to call again after an error (retry). */
    ensure: (type: SlotType) => Promise<void>
}

/** /api/picker/{champions,items,summoners} row (items carry more, unused here). */
interface OptionPayload {
    id: string
    name: string
    image: string | null
}

interface RunePerkPayload {
    id: number
    name: string
    icon: string | null
}

interface RuneTreePayload {
    id: number
    name: string
    icon: string | null
    slots: RunePerkPayload[][]
}

interface CatalogResponse {
    options?: OptionPayload[]
    trees?: RuneTreePayload[]
}

/**
 * Lazy in-memory catalogue behind the favorite picker: the first dialog open
 * of a type fetches its endpoint (canonical ?version=&lang= URL), then every
 * reopen is served from memory. A failed fetch parks the type in a retryable
 * error state instead of throwing into the component.
 */
export function usePickerCatalog(config: PickerCatalogConfig): PickerCatalog {
    const states = reactive<Record<SlotType, CatalogState>>({
        champion: { status: 'idle', entries: [] },
        item: { status: 'idle', entries: [] },
        rune: { status: 'idle', entries: [] },
        summoner: { status: 'idle', entries: [] },
    })

    async function ensure(type: SlotType): Promise<void> {
        const state = states[type]
        if (state.status === 'loading' || state.status === 'ready') {
            return
        }
        state.status = 'loading'
        try {
            const response = await fetch(catalogUrl(config, type))
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`)
            }
            state.entries = toEntries(type, (await response.json()) as CatalogResponse)
            state.status = 'ready'
        } catch {
            state.status = 'error'
        }
    }

    return { states, ensure }
}

function catalogUrl(config: PickerCatalogConfig, type: SlotType): string {
    const params = new URLSearchParams({ version: config.version, lang: config.lang })
    return `${config.endpoints[type]}?${params.toString()}`
}

function toEntries(type: SlotType, payload: CatalogResponse): PickerEntry[] {
    if (type === 'rune') {
        return runeEntries(payload.trees ?? [])
    }
    return (payload.options ?? []).map((option) => ({
        id: option.id,
        name: option.name,
        image: option.image,
        // The id joins the haystack so "flash" finds SummonerFlash-style ids too.
        searchText: normalizeSearchText(`${option.name} ${option.id}`),
    }))
}

/** Flatten trees into a display list: header row, then its perks in slot order. */
function runeEntries(trees: RuneTreePayload[]): PickerEntry[] {
    const entries: PickerEntry[] = []
    for (const tree of trees) {
        const treeId = String(tree.id)
        entries.push({
            id: treeId,
            name: tree.name,
            image: tree.icon,
            isGroup: true,
            searchText: normalizeSearchText(tree.name),
        })
        for (const slot of tree.slots) {
            for (const perk of slot) {
                entries.push({
                    id: String(perk.id),
                    name: perk.name,
                    image: perk.icon,
                    groupId: treeId,
                    searchText: normalizeSearchText(perk.name),
                })
            }
        }
    }
    return entries
}
