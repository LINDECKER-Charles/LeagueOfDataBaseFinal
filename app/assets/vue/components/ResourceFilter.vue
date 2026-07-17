<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'

/**
 * Client-side filter bar for a server-rendered resource grid. Vue owns only this
 * control bar (live search + multi-select tag facet + pagination); the grid stays
 * server-rendered so the rich per-type cards are preserved. Cards are read from
 * `#{gridId} > [data-search]` (searchable text) / `[data-tags]` (`|`-joined facet)
 * and shown/hidden imperatively. Tag matching is OR within the single facet.
 */
interface Labels {
    results: string // carries "%count%"
    empty: string
    clear: string
    prev: string
    next: string
    perPage: string
    all: string
}

const props = withDefaults(
    defineProps<{
        gridId: string
        placeholder?: string
        pageSize?: number
        pageSizes?: number[]
        labels: Labels
    }>(),
    { placeholder: 'Search…', pageSize: 12, pageSizes: () => [12, 24, 48] },
)

interface Card {
    el: HTMLElement
    search: string
    tags: string[]
}

const ALL = 0 // sentinel page size: one page holding every filtered card

const cards = ref<Card[]>([])
const tagUniverse = ref<string[]>([])
const query = ref('')
const selected = ref<Set<string>>(new Set())
const page = ref(1)
const size = ref(props.pageSize)

const filtered = computed(() => {
    const q = query.value.trim().toLowerCase()
    const sel = selected.value
    return cards.value.filter(
        (c) => (!q || c.search.includes(q)) && (sel.size === 0 || c.tags.some((t) => sel.has(t))),
    )
})
const effectiveSize = computed(() => (size.value === ALL ? Math.max(1, filtered.value.length) : size.value))
const totalPages = computed(() => Math.max(1, Math.ceil(filtered.value.length / effectiveSize.value)))
const resultLabel = computed(() => props.labels.results.replace('%count%', String(filtered.value.length)))
const sizeOptions = computed(() => [
    ...props.pageSizes.map((v) => ({ value: v, label: String(v) })),
    { value: ALL, label: props.labels.all },
])

/** Show only the current page of the filtered set; hide everything else. */
function apply(): void {
    if (page.value > totalPages.value) {
        page.value = 1
    }
    const start = (page.value - 1) * effectiveSize.value
    const shown = new Set(filtered.value.slice(start, start + effectiveSize.value).map((c) => c.el))
    for (const c of cards.value) {
        c.el.style.display = shown.has(c.el) ? '' : 'none'
    }
}

function setSize(value: number): void {
    size.value = value
    page.value = 1
}

function toggleTag(tag: string): void {
    const next = new Set(selected.value)
    next.has(tag) ? next.delete(tag) : next.add(tag)
    selected.value = next
    page.value = 1
}
function clearAll(): void {
    query.value = ''
    selected.value = new Set()
    page.value = 1
}
function go(delta: number): void {
    page.value = Math.min(totalPages.value, Math.max(1, page.value + delta))
}

/** Split "CriticalStrike" → "Critical Strike" for display only (matching uses the raw tag). */
function pretty(tag: string): string {
    return tag.replace(/([a-z])([A-Z])/g, '$1 $2')
}

const gridEl = ref<HTMLElement | null>(null)

onMounted(() => {
    const grid = document.getElementById(props.gridId)
    if (!grid) {
        return
    }
    gridEl.value = grid
    cards.value = Array.from(grid.querySelectorAll<HTMLElement>(':scope > [data-search]')).map((el) => ({
        el,
        search: (el.dataset.search ?? '').toLowerCase(),
        tags: (el.dataset.tags ?? '').split('|').filter(Boolean),
    }))
    const universe = new Set<string>()
    cards.value.forEach((c) => c.tags.forEach((t) => universe.add(t)))
    tagUniverse.value = Array.from(universe).sort((a, b) => a.localeCompare(b))
    apply()
    // Hand paint control to JS: the pre-hide CSS (first page only, no-JS fallback)
    // steps aside so inline display styles drive pagination from here on.
    grid.dataset.ready = 'true'
})

watch([filtered, page, size], apply)
onBeforeUnmount(() => {
    cards.value.forEach((c) => (c.el.style.display = ''))
    gridEl.value?.removeAttribute('data-ready')
})
</script>

<template>
    <div class="flex flex-col gap-3">
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <label class="relative w-full sm:max-w-xs">
                <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-text-muted"
                     viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true">
                    <circle cx="9" cy="9" r="6" /><path d="M14 14l4 4" stroke-linecap="round" />
                </svg>
                <input v-model="query" type="search" :placeholder="placeholder"
                       class="w-full border border-gold-deep/50 bg-void/70 py-2 pl-9 pr-3 font-mono text-sm text-text transition-colors placeholder:text-text-muted/70 focus:border-gold focus:outline-none" />
            </label>

            <div class="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 sm:justify-end">
                <div class="flex items-center gap-2">
                    <span class="hidden font-mono text-[11px] uppercase tracking-wider text-text-muted lg:inline">{{ labels.perPage }}</span>
                    <div class="flex items-center gap-1">
                        <button v-for="opt in sizeOptions" :key="opt.value" type="button"
                                class="pp-btn" :class="{ 'pp-btn--on': size === opt.value }"
                                :aria-pressed="size === opt.value" @click="setSize(opt.value)">{{ opt.label }}</button>
                    </div>
                </div>
                <span class="font-mono text-xs text-text-muted">{{ resultLabel }}</span>
                <div v-if="totalPages > 1" class="flex items-center gap-1">
                    <button type="button" class="filter-nav" :aria-label="labels.prev" :disabled="page === 1" @click="go(-1)">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
                    </button>
                    <span class="min-w-14 text-center font-mono text-xs text-text-muted">{{ page }} / {{ totalPages }}</span>
                    <button type="button" class="filter-nav" :aria-label="labels.next" :disabled="page === totalPages" @click="go(1)">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M9 6l6 6-6 6" /></svg>
                    </button>
                </div>
            </div>
        </div>

        <div v-if="tagUniverse.length" class="flex flex-wrap items-center gap-1.5">
            <button v-for="tag in tagUniverse" :key="tag" type="button"
                    class="filter-chip" :class="{ 'filter-chip--on': selected.has(tag) }"
                    :aria-pressed="selected.has(tag)" @click="toggleTag(tag)">{{ pretty(tag) }}</button>
            <button v-if="query || selected.size" type="button" class="filter-clear" @click="clearAll">{{ labels.clear }}</button>
        </div>

        <p v-if="filtered.length === 0" class="py-6 text-center font-mono text-sm text-text-muted">{{ labels.empty }}</p>
    </div>
</template>

<style scoped>
.filter-chip {
    padding: 0.2rem 0.6rem;
    font-family: ui-monospace, monospace;
    font-size: 0.72rem;
    color: var(--color-text-muted);
    border: 1px solid var(--color-gold-deep);
    background: color-mix(in srgb, var(--color-void) 60%, transparent);
    transition: color 0.2s var(--ease-hextech, ease), border-color 0.2s var(--ease-hextech, ease), background-color 0.2s var(--ease-hextech, ease);
}
.filter-chip:hover {
    color: var(--color-hex);
    border-color: color-mix(in srgb, var(--color-hex) 60%, transparent);
}
.filter-chip--on {
    color: var(--color-gold-bright);
    border-color: var(--color-gold);
    background: rgba(200, 170, 110, 0.16);
}
.filter-clear {
    padding: 0.2rem 0.55rem;
    font-family: ui-monospace, monospace;
    font-size: 0.72rem;
    color: var(--color-text-dim);
    text-decoration: underline;
    text-underline-offset: 2px;
}
.filter-clear:hover { color: var(--color-gold-bright); }
.pp-btn {
    min-width: 2rem;
    height: 2rem;
    padding: 0 0.5rem;
    font-family: ui-monospace, monospace;
    font-size: 0.75rem;
    color: var(--color-text-muted);
    border: 1px solid color-mix(in srgb, var(--color-gold-deep) 55%, transparent);
    background: color-mix(in srgb, var(--color-void) 60%, transparent);
    transition: color 0.2s ease, border-color 0.2s ease, background-color 0.2s ease;
}
.pp-btn:hover { color: var(--color-hex); border-color: color-mix(in srgb, var(--color-hex) 60%, transparent); }
.pp-btn--on {
    color: var(--color-gold-bright);
    border-color: var(--color-gold);
    background: rgba(200, 170, 110, 0.16);
}

.filter-nav {
    display: grid;
    place-items: center;
    width: 2rem;
    height: 2rem;
    color: var(--color-gold);
    border: 1px solid var(--color-gold-deep);
    background: var(--color-void);
    transition: color 0.2s ease, border-color 0.2s ease;
}
.filter-nav:hover:not(:disabled) { color: var(--color-gold-bright); border-color: var(--color-gold); }
.filter-nav:disabled { opacity: 0.4; cursor: not-allowed; }
.filter-nav svg { width: 1.1rem; height: 1.1rem; }
</style>
