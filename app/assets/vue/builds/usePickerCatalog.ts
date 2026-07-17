import { ref, type Ref } from 'vue'

/**
 * Lazy fetch of one picker catalog with a module-scoped memory cache (shared
 * across editor mounts of the session — Turbo keeps the page alive) and
 * loading / error / retry states for the UI.
 */

interface PickerCatalog<T> {
    data: Ref<T | null>
    isLoading: Ref<boolean>
    hasError: Ref<boolean>
    load: () => Promise<void>
    retry: () => Promise<void>
}

/** Resolved payloads by full URL — one fetch per (endpoint, version, lang). */
const payloadCache = new Map<string, unknown>()

/** Test hook: the cache is module-scoped, specs need a clean slate. */
export function clearPickerCatalogCache(): void {
    payloadCache.clear()
}

export function usePickerCatalog<T>(url: () => string, extract: (payload: unknown) => T): PickerCatalog<T> {
    const data = ref<T | null>(null) as Ref<T | null>
    const isLoading = ref(false)
    const hasError = ref(false)

    async function load(): Promise<void> {
        if (data.value !== null || isLoading.value) return
        const target = url()
        const cached = payloadCache.get(target)
        if (cached !== undefined) {
            data.value = extract(cached)
            return
        }

        isLoading.value = true
        hasError.value = false
        try {
            const response = await fetch(target, { headers: { Accept: 'application/json' } })
            if (!response.ok) throw new Error(`HTTP ${response.status}`)
            const payload: unknown = await response.json()
            payloadCache.set(target, payload)
            data.value = extract(payload)
        } catch {
            hasError.value = true
        } finally {
            isLoading.value = false
        }
    }

    async function retry(): Promise<void> {
        hasError.value = false
        await load()
    }

    return { data, isLoading, hasError, load, retry }
}
