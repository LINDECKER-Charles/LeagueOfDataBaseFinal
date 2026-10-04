import { reactive } from 'vue'
import { normalizeSearchText } from '../search/normalizeSearchText'
import type { PickerEntry } from './filterOptions'

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

/** Shared answer for a champion nobody asked to load yet — read, never written. */
const IDLE_SKIN_STATE: CatalogState = { status: 'idle', entries: [] }

export interface SkinCatalog {
    champions: CatalogState
    /** Read-only skin state of a champion; an unloaded one reads as idle. */
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

    return {
        champions,
        // Pure read: it is called from a computed, which must not write state.
        // Reading a missing key still tracks it, so the registration done by
        // loadSkins re-runs the computed.
        skinsFor: (championId) => skins[championId] ?? IDLE_SKIN_STATE,
        ensureChampions: () => loadChampions(champions, config),
        ensureSkins: (championId) => loadSkins(skins, config, championId),
    }
}

async function loadChampions(state: CatalogState, config: SkinCatalogConfig): Promise<void> {
    if (state.status === 'loading' || state.status === 'ready') {
        return
    }
    state.status = 'loading'
    try {
        const payload = await fetchJson<ChampionsResponse>(config.championsEndpoint, config)
        state.entries = (payload.options ?? []).map((option) => ({
            id: option.id,
            name: option.name,
            image: option.image,
            // The id joins the haystack so ids and display names both match.
            searchText: normalizeSearchText(`${option.name} ${option.id}`),
        }))
        state.status = 'ready'
    } catch {
        state.status = 'error'
    }
}

async function loadSkins(
    skins: Record<string, CatalogState>,
    config: SkinCatalogConfig,
    championId: string,
): Promise<void> {
    skins[championId] ??= { status: 'idle', entries: [] }
    const state = skins[championId] as CatalogState
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

async function fetchJson<T>(endpoint: string, config: SkinCatalogConfig): Promise<T> {
    const separator = endpoint.includes('?') ? '&' : '?'
    const params = new URLSearchParams({ version: config.version, lang: config.lang })
    const response = await fetch(`${endpoint}${separator}${params.toString()}`)
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`)
    }
    return (await response.json()) as T
}
