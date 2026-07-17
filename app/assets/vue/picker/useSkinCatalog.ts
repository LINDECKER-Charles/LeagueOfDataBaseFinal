import { reactive } from 'vue'
import { normalizeSearchText, type PickerEntry } from './filterOptions'

export type CatalogStatus = 'idle' | 'loading' | 'ready' | 'error'

export interface CatalogState {
    status: CatalogStatus
    entries: SkinPickerEntry[]
}

/** A skin row carries its wide banner art on top of the base picker fields. */
export interface SkinPickerEntry extends PickerEntry {
    banner?: string
}

export interface SkinCatalogConfig {
    championsEndpoint: string
    skinsEndpoint: string
    version: string
    lang: string
}

interface OptionPayload {
    id: string
    name: string
    image: string | null
}

interface SkinPayload {
    id: string
    num: number
    name: string
    image: string | null
    banner: string | null
}

interface ChampionsResponse {
    options?: OptionPayload[]
}

interface SkinsResponse {
    skins?: SkinPayload[]
}

export interface SkinCatalog {
    champions: CatalogState
    /** Reactive per-champion skin state (created idle on first access). */
    skinsFor: (championId: string) => CatalogState
    ensureChampions: () => Promise<void>
    ensureSkins: (championId: string) => Promise<void>
}

/**
 * Lazy catalogue behind the two-step skin banner picker: the champion list is
 * fetched once, each champion's skins on first open of that champion, then both
 * are memoised. A failed fetch parks the relevant state in a retryable error
 * instead of throwing into the component. Fetch URLs carry the canonical
 * ?version=&lang= so the responses stay shared-cacheable.
 */
export function useSkinCatalog(config: SkinCatalogConfig): SkinCatalog {
    const champions = reactive<CatalogState>({ status: 'idle', entries: [] })
    const skins = reactive<Record<string, CatalogState>>({})

    function skinsFor(championId: string): CatalogState {
        if (!skins[championId]) {
            skins[championId] = { status: 'idle', entries: [] }
        }
        return skins[championId]
    }

    async function ensureChampions(): Promise<void> {
        if (champions.status === 'loading' || champions.status === 'ready') {
            return
        }
        champions.status = 'loading'
        try {
            const payload = await fetchJson<ChampionsResponse>(config.championsEndpoint, config)
            champions.entries = (payload.options ?? []).map((option) => ({
                id: option.id,
                name: option.name,
                image: option.image,
                // The id joins the haystack so ids and display names both match.
                searchText: normalizeSearchText(`${option.name} ${option.id}`),
            }))
            champions.status = 'ready'
        } catch {
            champions.status = 'error'
        }
    }

    async function ensureSkins(championId: string): Promise<void> {
        const state = skinsFor(championId)
        if (state.status === 'loading' || state.status === 'ready') {
            return
        }
        state.status = 'loading'
        try {
            const url = `${config.skinsEndpoint}?champion=${encodeURIComponent(championId)}`
            const payload = await fetchJson<SkinsResponse>(url, config)
            state.entries = (payload.skins ?? []).map((skin) => ({
                id: skin.id,
                name: skin.name,
                image: skin.image,
                banner: skin.banner ?? undefined,
                searchText: normalizeSearchText(skin.name),
            }))
            state.status = 'ready'
        } catch {
            state.status = 'error'
        }
    }

    return { champions, skinsFor, ensureChampions, ensureSkins }
}

async function fetchJson<T>(endpoint: string, config: SkinCatalogConfig): Promise<T> {
    const separator = endpoint.includes('?') ? '&' : '?'
    const params = new URLSearchParams({ version: config.version, lang: config.lang })
    const response = await fetch(`${endpoint}${separator}${params.toString()}`)
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
    }
    return (await response.json()) as T
}
