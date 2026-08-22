<script setup lang="ts">
import { computed, ref } from 'vue'
import type { FacetDefinition, FacetState } from '../../../filter/facets'
import type { FacetCounts } from '../../../filter/facetCounts'
import type { FacetUniverse } from '../../../filter/useCardGrid'
import { formatTemplate, pluralTemplate } from '../../../i18n/formatTemplate'
import FacetPanel from './FacetPanel.vue'

/**
 * The facets as a mobile bottom sheet — a native <dialog> (top layer, inert
 * background, Escape and Android-back dismissal for free). Filtering stays
 * live behind it; the gold call to action only closes the sheet, reading how
 * many results wait.
 */
const props = defineProps<{
    facets: readonly FacetDefinition[]
    state: FacetState
    universe: FacetUniverse
    counts: FacetCounts
    activeCount: number
    matching: number
    total: number
    hasFilters: boolean
    labels: {
        filters: string
        clear: string
        showResults: string
        showResultsOne: string
        matchMode: string
        matchAny: string
        matchAll: string
        min: string
        max: string
    }
}>()

const emit = defineEmits<{ clearAll: [] }>()

const dialog = ref<HTMLDialogElement | null>(null)
const cta = computed(() => formatTemplate(
    pluralTemplate(props.matching, props.labels.showResultsOne, props.labels.showResults),
    { count: props.matching },
))

function open(): void {
    dialog.value?.showModal()
}

function close(): void {
    dialog.value?.close()
}

/* The dialog's own padding is also the dialog as an event target: only a
   click outside its box is the backdrop. */
function onClick(event: MouseEvent): void {
    const box = dialog.value?.getBoundingClientRect()
    if (!box || event.target !== dialog.value) return
    const isInside = event.clientX >= box.left && event.clientX <= box.right
        && event.clientY >= box.top && event.clientY <= box.bottom
    if (!isInside) close()
}

defineExpose({ open, close })
</script>

<template>
    <dialog ref="dialog" class="filter-sheet" :aria-label="labels.filters" @click="onClick">
        <div class="filter-sheet__handle" aria-hidden="true"></div>
        <div class="filter-sheet__head">
            <span class="filter-marker filter-marker--gold" aria-hidden="true"></span>
            <span class="filter-console__title">{{ labels.filters }}</span>
            <b v-if="activeCount" class="filter-badge">{{ activeCount }}</b>
            <span class="filter-gauge__value">{{ matching }} / {{ total }}</span>
        </div>
        <div class="filter-sheet__body">
            <FacetPanel :facets="facets" :state="state" :universe="universe" :counts="counts"
                        :labels="labels" />
        </div>
        <div class="filter-sheet__footer">
            <button type="button" class="filter-sheet__clear" :disabled="!hasFilters"
                    @click="emit('clearAll')">{{ labels.clear }}</button>
            <button type="button" class="hx-btn-gold filter-sheet__done" @click="close">{{ cta }}</button>
        </div>
    </dialog>
</template>
