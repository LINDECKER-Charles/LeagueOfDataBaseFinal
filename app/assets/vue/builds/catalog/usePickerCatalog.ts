import { ref, type Ref } from 'vue'

/**
 * Lazy fetch of one picker catalog with a module-scoped memory cache (shared
 * across editor mounts of the session — Turbo keeps the page alive) and
 * loading / error / retry states for the UI. `reload()` re-resolves against the
 * CURRENT url() — the editor calls it when its version/mode context changes;
 * already-seen URLs answer from cache, so switching back is instant.
 */

export interface PickerCatalog<T> {
    data: Ref<T | null>
    isLoading: Ref<boolean>
    hasError: Ref<boolean>
    load: () => Promise<void>
    retry: () => Promise<void>
    reload: () => Promise<void>
}

/** Everything one catalog needs to resolve itself, refs included. */
interface CatalogState<T> {
    data: Ref<T | null>
    isLoading: Ref<boolean>
    hasError: Ref<boolean>
    url: () => string
    extract: (payload: unknown) => T
    // Monotonic fetch token: a reload during an in-flight fetch must win — the
    // stale response is dropped instead of clobbering the newer context.
    seq: number
}

/** Resolved payloads by full URL — one fetch per (endpoint, version, lang, mode). */
const payloadCache = new Map<string, unknown>()

/** Test hook: the cache is module-scoped, specs need a clean slate. */
export function clearPickerCatalogCache(): void {
    payloadCache.clear()
}

export function usePickerCatalog<T>(
    url: () => string,
    extract: (payload: unknown) => T,
): PickerCatalog<T> {
    const state: CatalogState<T> = {
        data: ref(null) as Ref<T | null>,
        isLoading: ref(false),
        hasError: ref(false),
        url,
        extract,
        seq: 0,
    }

    return {
        data: state.data,
        isLoading: state.isLoading,
        hasError: state.hasError,
        load: () => load(state),
        retry: () => retry(state),
        reload: () => reload(state),
    }
}

/** First load only: an already-resolved or in-flight catalog is left alone. */
async function load<T>(state: CatalogState<T>): Promise<void> {
    if (state.data.value !== null || state.isLoading.value) return
    await fetchInto(state, ++state.seq)
}

async function retry<T>(state: CatalogState<T>): Promise<void> {
    state.hasError.value = false
    await load(state)
}

/** Re-resolve against the CURRENT url(): the (version, mode) context changed. */
async function reload<T>(state: CatalogState<T>): Promise<void> {
    state.data.value = null
    state.hasError.value = false
    await fetchInto(state, ++state.seq)
}

async function fetchInto<T>(state: CatalogState<T>, seq: number): Promise<void> {
    const target = state.url()
    const cached = payloadCache.get(target)
    if (cached !== undefined) {
        state.data.value = state.extract(cached)
        return
    }

    state.isLoading.value = true
    state.hasError.value = false
    try {
        const response = await fetch(target, { headers: { Accept: 'application/json' } })
        if (!response.ok) throw new Error(`HTTP ${response.status}`)
        const payload: unknown = await response.json()
        payloadCache.set(target, payload)
        if (seq === state.seq) state.data.value = state.extract(payload)
    } catch {
        if (seq === state.seq) state.hasError.value = true
    } finally {
        if (seq === state.seq) state.isLoading.value = false
    }
}
