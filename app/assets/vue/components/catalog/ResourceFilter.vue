<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import type { FacetDefinition } from '../../filter/facets'
import { useCardGrid } from '../../filter/useCardGrid'
import { useFacetState } from '../../filter/useFacetState'
import { writeFilterUrl } from '../../filter/url/urlState'
import { useUrlSync } from '../../filter/url/useUrlSync'
import { PAGE_SIZE_ALL, selectVisibleCards } from '../../filter/visibleCards'
import { formatTemplate } from '../../i18n/formatTemplate'
import ActiveFilters from './facets/ActiveFilters.vue'
import FacetChoice from './facets/FacetChoice.vue'
import FacetPanel from './facets/FacetPanel.vue'

/**
 * Client-side filter bar for a server-rendered resource grid. Vue owns only
 * this control bar (live search, per-category facets, pagination); the grid
 * stays server-rendered so the rich per-type cards are preserved. The state
 * mirrors the URL both ways, so a filtered list is a link. Reading/painting
 * the grid lives in useCardGrid, the visibility rule in selectVisibleCards,
 * the facet transitions in useFacetState; this SFC is binding and markup.
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
    advanced: string
    matchAny: string
    matchAll: string
    min: string
    max: string
    copy: string
    copied: string
    copyError: string
    active: string
}

const props = withDefaults(
    defineProps<{
        gridId: string
        placeholder?: string
        pageSize?: number
        pageSizes?: number[]
        facets?: FacetDefinition[]
        labels: Labels
    }>(),
    { placeholder: 'Search…', pageSize: 12, pageSizes: () => [12, 24, 48], facets: () => [] },
)

const schema = props.facets
const grid = useCardGrid(props.gridId, schema)
const url = useUrlSync({
    schema,
    defaultSize: props.pageSize,
    current: () => ({ query: query.value, facets: store.facets.value, page: page.value, size: size.value }),
})
const query = ref(url.initial.query)
const store = useFacetState(url.initial.facets)
const page = ref(url.initial.page)
const size = ref(url.initial.size)
const isAdvancedOpen = ref(false)
const universe = grid.universe

const selection = computed(() =>
    selectVisibleCards(grid.cards.value, {
        query: query.value,
        facets: store.facets.value,
        schema,
        page: page.value,
        pageSize: size.value,
    }),
)

/** Facets the grid can actually be narrowed by (something to choose from). */
const offered = computed(() => schema.filter((facet) => isOffered(facet)))
const primaryFacets = computed(() => offered.value.filter((facet) => facet.primary))
const advancedFacets = computed(() => offered.value.filter((facet) => !facet.primary))
const hasFilters = computed(() => query.value.trim() !== '' || store.activeCount.value > 0)
const resultLabel = computed(
    () => formatTemplate(props.labels.results, { count: selection.value.matching.length }),
)
const sizeOptions = computed(() => [
    ...props.pageSizes.map((v) => ({ value: v, label: String(v) })),
    { value: PAGE_SIZE_ALL, label: props.labels.all },
])
const shareUrl = computed(() =>
    window.location.origin + window.location.pathname + writeFilterUrl(
        window.location.search,
        { query: query.value, facets: store.facets.value, page: page.value, size: size.value },
        schema,
        props.pageSize,
    ),
)

function isOffered(facet: FacetDefinition): boolean {
    const u = universe.value
    switch (facet.kind) {
        case 'choice':
            return (u.present[facet.key]?.size ?? 0) > 0
        case 'range':
            return u.bounds[facet.key] !== undefined && u.bounds[facet.key].min < u.bounds[facet.key].max
        case 'toggle':
            return (u.flagged[facet.key] ?? 0) > 0
    }
}

function choiceSelection(key: string) {
    const current = store.facets.value[key]
    return current !== undefined && typeof current === 'object' && 'values' in current ? current : undefined
}

/** Repaint the grid, following the page reset the selection rule may have made. */
function paintCurrentPage(): void {
    page.value = selection.value.page
    grid.show(selection.value.visible)
}

function setSize(value: number): void {
    size.value = value
    page.value = 1
}
function movePage(delta: number): void {
    page.value = Math.min(selection.value.pageCount, Math.max(1, page.value + delta))
}
function clearAll(): void {
    query.value = ''
    store.clearAll()
    page.value = 1
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
    if (event.target === sheet.value) closeSheet() // backdrop click
}

// Paint BEFORE handing control over, so the grid never flashes unpaginated.
onMounted(() => {
    grid.scan()
    paintCurrentPage()
    grid.markReady()
})

watch(selection, paintCurrentPage)
// Any engagement resets the page; the URL follows every change (throttled).
watch([query, store.facets, size], () => (page.value = 1))
watch([query, store.facets, page, size], url.sync)
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

        <!-- Primary facets inline from md up; everything in a bottom sheet below. -->
        <div v-if="offered.length" class="hidden flex-col gap-2 md:flex">
            <FacetChoice v-for="facet in primaryFacets" :key="facet.key" :facet="facet" inline
                         :selection="choiceSelection(facet.key)"
                         :present="universe.present[facet.key] ?? new Set()"
                         :labels="labels"
                         @toggle="(value) => store.toggleChoice(facet, value)"
                         @match-all="(all) => store.setMatchAll(facet.key, all)" />
            <div class="flex flex-wrap items-center gap-2">
                <button v-if="advancedFacets.length" type="button" class="filter-trigger"
                        :aria-expanded="isAdvancedOpen" @click="isAdvancedOpen = !isAdvancedOpen">
                    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.6"
                         stroke-linecap="round" aria-hidden="true"><path d="M3 5h14M6 10h8M8.5 15h3" /></svg>
                    {{ labels.advanced }}
                </button>
            </div>
            <FacetPanel v-if="isAdvancedOpen && advancedFacets.length" class="facet-panel--desktop"
                        :facets="advancedFacets" :state="store.facets.value" :universe="universe"
                        :labels="labels"
                        @toggle-choice="store.toggleChoice" @match-all="store.setMatchAll"
                        @range="store.setRange" @toggle="store.setToggle" />
        </div>

        <div v-if="offered.length" class="flex items-center gap-2 md:hidden">
            <button type="button" class="filter-trigger" @click="openSheet">
                <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.6"
                     stroke-linecap="round" aria-hidden="true"><path d="M3 5h14M6 10h8M8.5 15h3" /></svg>
                {{ labels.filters }}
                <b v-if="store.activeCount.value">{{ store.activeCount.value }}</b>
            </button>
        </div>

        <ActiveFilters v-if="hasFilters" :schema="schema" :state="store.facets.value" :query="query"
                       :share-url="shareUrl" :labels="labels"
                       @clear-facet="store.clearFacet" @clear-query="query = ''" @clear-all="clearAll" />

        <dialog ref="sheet" class="filter-sheet" :aria-label="labels.filters" @click="onSheetClick">
            <div class="filter-sheet__handle" aria-hidden="true"></div>
            <p class="eyebrow mb-3">{{ labels.filters }}</p>
            <div class="filter-sheet__body">
                <FacetPanel :facets="offered" :state="store.facets.value" :universe="universe"
                            :labels="labels" collapsed
                            @toggle-choice="store.toggleChoice" @match-all="store.setMatchAll"
                            @range="store.setRange" @toggle="store.setToggle" />
            </div>
            <div class="filter-sheet__footer">
                <button type="button" class="filter-clear" :disabled="!hasFilters"
                        @click="clearAll">{{ labels.clear }}</button>
                <span class="font-mono text-xs text-text-muted">{{ resultLabel }}</span>
                <button type="button" class="filter-done" @click="closeSheet">{{ labels.close }}</button>
            </div>
        </dialog>

        <p v-if="selection.matching.length === 0"
           class="py-6 text-center font-mono text-sm text-text-muted">{{ labels.empty }}</p>
    </div>
</template>
