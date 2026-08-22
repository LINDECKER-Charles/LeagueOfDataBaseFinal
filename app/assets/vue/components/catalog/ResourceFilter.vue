<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import type { FacetDefinition } from '../../filter/facets'
import { countFacetOptions } from '../../filter/facetCounts'
import { offeredFacets, useCardGrid } from '../../filter/useCardGrid'
import { provideFacetStore, useFacetState } from '../../filter/useFacetState'
import { writeFilterUrl } from '../../filter/url/urlState'
import { useUrlSync } from '../../filter/url/useUrlSync'
import { PAGE_SIZE_ALL, selectVisibleCards } from '../../filter/visibleCards'
import { useSearchShortcut } from '../../search/useSearchShortcut'
import CopyLink from '../ui/CopyLink.vue'
import ActiveFilters from './facets/ActiveFilters.vue'
import FacetPanel from './facets/FacetPanel.vue'
import FilterSearch from './facets/FilterSearch.vue'
import FilterSheet from './facets/FilterSheet.vue'
import FilterToolbar from './facets/FilterToolbar.vue'

/**
 * Client-side filter of a server-rendered resource grid. Vue owns the
 * controls only — the grid stays server-rendered so the rich per-type cards
 * are preserved. The island is mounted in the rail slot of the list layout
 * (components/codex/list_filter.html.twig) and teleports the rest into the
 * slots that layout reserves around the grid: `#<gridId>-bar` (mobile search
 * + sheet trigger), `#<gridId>-head` (results head + active chips) and
 * `#<gridId>-empty`. The state mirrors the URL both ways, so a filtered list
 * is a link. Reading/painting the grid lives in useCardGrid, the visibility
 * rule in selectVisibleCards, the counts in facetCounts, the facet
 * transitions in useFacetState; this SFC is binding and markup.
 */
interface Labels {
    results: string // carries "%count%"
    resultsOne: string
    page: string // carries "%page%" and "%count%"
    gauge: string
    empty: string
    emptyTitle: string
    emptyCta: string
    clear: string
    clearAll: string
    prev: string
    next: string
    perPage: string
    all: string
    filters: string
    showResults: string // carries "%count%"
    showResultsOne: string
    matchMode: string
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
const universe = grid.universe
provideFacetStore(store)

const criteria = computed(() => ({ query: query.value, facets: store.facets.value, schema }))
const selection = computed(() =>
    selectVisibleCards(grid.cards.value, { ...criteria.value, page: page.value, pageSize: size.value }),
)
const counts = computed(() => countFacetOptions(grid.cards.value, criteria.value))
const offered = computed(() => offeredFacets(schema, universe.value))
const total = computed(() => grid.cards.value.length)
const matching = computed(() => selection.value.matching.length)
const gaugePercent = computed(() => (total.value ? Math.round((matching.value / total.value) * 100) : 0))
const hasFilters = computed(() => query.value.trim() !== '' || store.activeCount.value > 0)
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
const copyLabels = computed(() => ({
    copy: props.labels.copy, copied: props.labels.copied, error: props.labels.copyError,
}))

const railSearch = ref<InstanceType<typeof FilterSearch> | null>(null)
const barSearch = ref<InstanceType<typeof FilterSearch> | null>(null)
const sheet = ref<InstanceType<typeof FilterSheet> | null>(null)
useSearchShortcut(() => [railSearch.value?.field ?? null, barSearch.value?.field ?? null])
/* The island's empty state is for a grid that has cards but none matching;
   a list the server rendered empty already carries its own. */
const showsEmpty = computed(() => total.value > 0 && matching.value === 0)

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
    <div class="filter-console" :class="{ 'filter-console--engaged': hasFilters }">
        <span class="filter-console__glow" aria-hidden="true"></span>
        <div class="filter-console__head">
            <span class="filter-marker filter-marker--gold" aria-hidden="true"></span>
            <span class="filter-console__title">{{ labels.filters }}</span>
            <b v-if="store.activeCount.value" class="filter-badge">{{ store.activeCount.value }}</b>
            <button v-if="hasFilters" type="button" class="filter-clear filter-console__clear"
                    @click="clearAll">{{ labels.clearAll }}</button>
        </div>
        <div class="filter-console__body">
            <FilterSearch ref="railSearch" v-model="query" :placeholder="placeholder" with-hint />
            <div class="filter-gauge">
                <div class="filter-gauge__head">
                    <span class="facet__label">{{ labels.gauge }}</span>
                    <span class="filter-gauge__value">{{ matching }} / {{ total }}</span>
                </div>
                <div class="filter-gauge__track">
                    <span class="filter-gauge__fill" :style="{ width: `${gaugePercent}%` }"></span>
                </div>
            </div>
            <FacetPanel v-if="offered.length" :facets="offered" :state="store.facets.value"
                        :universe="universe" :counts="counts" :labels="labels" />
        </div>
        <div class="filter-console__foot">
            <CopyLink class="filter-console__share" :url="shareUrl" :labels="copyLabels" />
        </div>
    </div>

    <Teleport :to="`#${gridId}-bar`">
        <div class="filter-mobilebar">
            <FilterSearch ref="barSearch" v-model="query" :placeholder="placeholder"
                          class="filter-search--bar" />
            <button v-if="offered.length" type="button" class="filter-trigger" @click="sheet?.open()">
                <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.6"
                     stroke-linecap="round" aria-hidden="true"><path d="M3 5h14M6 10h8M8.5 15h3" /></svg>
                {{ labels.filters }}
                <b v-if="store.activeCount.value">{{ store.activeCount.value }}</b>
            </button>
        </div>
    </Teleport>

    <Teleport :to="`#${gridId}-head`">
        <FilterToolbar :matching="matching" :page="page" :page-count="selection.pageCount"
                       :size="size" :size-options="sizeOptions" :labels="labels"
                       @size="setSize" @move="movePage" />
        <ActiveFilters v-if="hasFilters" :schema="schema" :state="store.facets.value" :query="query"
                       :labels="labels"
                       @clear-facet="store.clearFacet" @clear-query="query = ''" @clear-all="clearAll" />
    </Teleport>

    <Teleport :to="`#${gridId}-empty`">
        <div v-if="showsEmpty" class="filter-empty">
            <span class="filter-marker" aria-hidden="true"></span>
            <p class="filter-empty__title">{{ labels.emptyTitle }}</p>
            <p class="filter-empty__text">{{ labels.empty }}</p>
            <button type="button" class="hx-btn" @click="clearAll">{{ labels.emptyCta }}</button>
        </div>
    </Teleport>

    <!-- Out of the rail slot, which is display:none below the desktop breakpoint
         and would leave the top-layer dialog without a box. -->
    <Teleport to="body">
        <FilterSheet v-if="offered.length" ref="sheet" :facets="offered" :state="store.facets.value"
                     :universe="universe" :counts="counts" :active-count="store.activeCount.value"
                     :matching="matching" :total="total" :has-filters="hasFilters" :labels="labels"
                     @clear-all="clearAll" />
    </Teleport>
</template>
