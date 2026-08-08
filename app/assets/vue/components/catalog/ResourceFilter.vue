<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useCardGrid } from '../../filter/useCardGrid'
import { PAGE_SIZE_ALL, selectVisibleCards } from '../../filter/visibleCards'
import { formatTemplate } from '../../i18n/formatTemplate'

/**
 * Client-side filter bar for a server-rendered resource grid. Vue owns only this
 * control bar (live search + multi-select tag facet + pagination); the grid stays
 * server-rendered so the rich per-type cards are preserved. Reading and painting
 * that grid lives in {@link useCardGrid}, the visibility rule in
 * {@link selectVisibleCards}; this SFC is binding and markup.
 */
interface Labels {
    results: string // carries "%count%"
    empty: string
    clear: string
    prev: string
    next: string
    perPage: string
    all: string
    filters: string
    close: string
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

const grid = useCardGrid(props.gridId)
// Top-level alias: nested refs are NOT auto-unwrapped inside a plain object.
const facetTags = grid.tags
const query = ref('')
const selected = ref<Set<string>>(new Set())
const page = ref(1)
const size = ref(props.pageSize)

const selection = computed(() =>
    selectVisibleCards(grid.cards.value, {
        query: query.value,
        tags: selected.value,
        page: page.value,
        pageSize: size.value,
    }),
)
const resultLabel = computed(
    () => formatTemplate(props.labels.results, { count: selection.value.matching.length }),
)
const sizeOptions = computed(() => [
    ...props.pageSizes.map((v) => ({ value: v, label: String(v) })),
    { value: PAGE_SIZE_ALL, label: props.labels.all },
])

/** Repaint the grid, following the page reset the selection rule may have made. */
function paintCurrentPage(): void {
    page.value = selection.value.page
    grid.show(selection.value.visible)
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
function movePage(delta: number): void {
    page.value = Math.min(selection.value.pageCount, Math.max(1, page.value + delta))
}

/** Split "CriticalStrike" → "Critical Strike" for display only (matching uses the raw tag). */
function humanizeTag(tag: string): string {
    return tag.replace(/([a-z])([A-Z])/g, '$1 $2')
}

/* Mobile facet sheet — native <dialog> (top layer, inert background, Escape
   and Android-back dismissal for free). Filtering stays live; "close" is the
   only commit action. */
const sheet = ref<HTMLDialogElement | null>(null)

function openSheet(): void {
    sheet.value?.showModal()
}
function closeSheet(): void {
    sheet.value?.close()
}
function onSheetClick(event: MouseEvent): void {
    if (event.target === sheet.value) {
        closeSheet() // backdrop click
    }
}

// Paint BEFORE handing control over, so the grid never flashes unpaginated.
onMounted(() => {
    grid.scan()
    paintCurrentPage()
    grid.markReady()
})

watch(selection, paintCurrentPage)
</script>

<template>
    <div class="flex flex-col gap-3">
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <label class="relative w-full sm:max-w-xs">
                <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2
                            text-text-muted"
                     viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.7"
                     aria-hidden="true">
                    <circle cx="9" cy="9" r="6" /><path d="M14 14l4 4" stroke-linecap="round" />
                </svg>
                <input v-model="query" type="search" :placeholder="placeholder"
                       class="w-full border border-gold-deep/50 bg-void/70 py-2 pl-9 pr-3 font-mono
                              text-sm text-text transition-colors placeholder:text-text-muted/70
                              focus:border-gold focus:outline-none" />
            </label>

            <div class="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 sm:justify-end">
                <div class="flex items-center gap-2">
                    <span class="hidden font-mono text-[11px] uppercase tracking-wider
                                 text-text-muted lg:inline">{{ labels.perPage }}</span>
                    <div class="flex items-center gap-1">
                        <button v-for="opt in sizeOptions" :key="opt.value" type="button"
                                class="pp-btn" :class="{ 'pp-btn--on': size === opt.value }"
                                :aria-pressed="size === opt.value"
                                @click="setSize(opt.value)">{{ opt.label }}</button>
                    </div>
                </div>
                <span class="font-mono text-xs text-text-muted">{{ resultLabel }}</span>
                <div v-if="selection.pageCount > 1" class="flex items-center gap-1">
                    <button type="button" class="filter-nav" :aria-label="labels.prev"
                            :disabled="page === 1" @click="movePage(-1)">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                             aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
                    </button>
                    <span class="min-w-14 text-center font-mono text-xs
                                 text-text-muted">{{ page }} / {{ selection.pageCount }}</span>
                    <button type="button" class="filter-nav" :aria-label="labels.next"
                            :disabled="page === selection.pageCount" @click="movePage(1)">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                             aria-hidden="true"><path d="M9 6l6 6-6 6" /></svg>
                    </button>
                </div>
            </div>
        </div>

        <!-- Facets: inline chips from md up; a thumb-friendly bottom sheet below. -->
        <div v-if="facetTags.length" class="hidden flex-wrap items-center gap-1.5 md:flex">
            <button v-for="tag in facetTags" :key="tag" type="button"
                    class="filter-chip" :class="{ 'filter-chip--on': selected.has(tag) }"
                    :aria-pressed="selected.has(tag)"
                    @click="toggleTag(tag)">{{ humanizeTag(tag) }}</button>
            <button v-if="query || selected.size" type="button" class="filter-clear"
                    @click="clearAll">{{ labels.clear }}</button>
        </div>

        <div v-if="facetTags.length" class="flex items-center gap-2 md:hidden">
            <button type="button" class="filter-trigger" @click="openSheet">
                <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.6"
                     stroke-linecap="round" aria-hidden="true">
                    <path d="M3 5h14M6 10h8M8.5 15h3" />
                </svg>
                {{ labels.filters }}
                <b v-if="selected.size">{{ selected.size }}</b>
            </button>
            <button v-if="query || selected.size" type="button" class="filter-clear"
                    @click="clearAll">{{ labels.clear }}</button>
        </div>

        <dialog ref="sheet" class="filter-sheet" :aria-label="labels.filters" @click="onSheetClick">
            <div class="filter-sheet__handle" aria-hidden="true"></div>
            <p class="eyebrow mb-4">{{ labels.filters }}</p>
            <div class="flex flex-wrap gap-2 overflow-y-auto">
                <button v-for="tag in facetTags" :key="tag" type="button"
                        class="filter-chip" :class="{ 'filter-chip--on': selected.has(tag) }"
                        :aria-pressed="selected.has(tag)"
                        @click="toggleTag(tag)">{{ humanizeTag(tag) }}</button>
            </div>
            <div class="mt-5 flex items-center justify-between gap-3">
                <button type="button" class="filter-clear" :disabled="!query && !selected.size"
                        @click="clearAll">
                    {{ labels.clear }}
                </button>
                <span class="font-mono text-xs text-text-muted">{{ resultLabel }}</span>
                <button type="button" class="filter-done"
                        @click="closeSheet">{{ labels.close }}</button>
            </div>
        </dialog>

        <p v-if="selection.matching.length === 0"
           class="py-6 text-center font-mono text-sm text-text-muted">{{ labels.empty }}</p>
    </div>
</template>

<style scoped src="./ResourceFilter.css"></style>
