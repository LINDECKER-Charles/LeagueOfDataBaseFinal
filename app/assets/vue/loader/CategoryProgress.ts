import { TYPE_TO_KEY, type ResourceKey } from './urls'

/**
 * Per-category image counts of one loader run. The stream announces a total per
 * server resource type on `start`, then one event per stored image; a manifest
 * row flips to "ready" the moment its own category drains — long before the run
 * as a whole completes.
 */
export class CategoryProgress {
    private readonly totals = new Map<ResourceKey, number>()
    private readonly counts = new Map<ResourceKey, number>()

    reset(): void {
        this.totals.clear()
        this.counts.clear()
    }

    /**
     * Adopt the totals of a `start` payload. A category announced empty is
     * already complete, so it is handed to `onComplete` right away.
     */
    seed(categories: Record<string, number>, onComplete: (key: ResourceKey) => void): void {
        for (const [type, count] of Object.entries(categories)) {
            const key = TYPE_TO_KEY[type]
            if (!key) continue
            this.totals.set(key, count)
            this.counts.set(key, 0)
            if (count === 0) onComplete(key)
        }
    }

    /** Count one landed image; true when its category has just drained. */
    record(key: ResourceKey): boolean {
        const seen = (this.counts.get(key) ?? 0) + 1
        this.counts.set(key, seen)

        return seen >= (this.totals.get(key) ?? 0)
    }
}
