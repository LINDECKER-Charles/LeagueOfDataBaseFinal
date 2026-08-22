import { onBeforeUnmount } from 'vue'
import type { FacetDefinition } from '../facets'
import { syncLocation } from './turboLocation'
import { parseFilterUrl, writeFilterUrl, type FilterUrlState } from './urlState'

/**
 * Keeps the page URL equal to the filter state. Reads once at mount (a shared
 * link, a Turbo restore); writes on a trailing throttle — Safari refuses more
 * than 100 history rewrites per 30 s, and a slider drag alone would exceed it.
 * A refused rewrite never reaches the filter: the state is the truth, the URL
 * catches up on the next tick.
 */
const WRITE_THROTTLE_MS = 300

export interface UrlSyncOptions {
    schema: readonly FacetDefinition[]
    defaultSize: number
    /** The state to mirror, read at write time. */
    current: () => FilterUrlState
}

export interface UrlSync {
    /** The state the URL held at mount. */
    initial: FilterUrlState
    /** Request a rewrite; coalesced, never more than one per throttle window. */
    sync: () => void
}

export function useUrlSync(options: UrlSyncOptions): UrlSync {
    let timer: ReturnType<typeof setTimeout> | null = null

    const write = (): void => {
        timer = null
        try {
            syncLocation(
                window.location.pathname
                    + writeFilterUrl(window.location.search, options.current(), options.schema, options.defaultSize)
                    + window.location.hash,
            )
        } catch {
            // Rate-limited or sandboxed history: the next sync retries.
        }
    }

    onBeforeUnmount(() => {
        if (timer !== null) clearTimeout(timer)
    })

    return {
        initial: parseFilterUrl(window.location.search, options.schema, options.defaultSize),
        sync: () => {
            if (timer === null) timer = setTimeout(write, WRITE_THROTTLE_MS)
        },
    }
}
