<script setup lang="ts">
import { computed } from 'vue'
import {
    isChoiceSelection,
    isRangeSelection,
    type FacetDefinition,
    type FacetState,
} from '../../../filter/facets'
import CopyLink from '../../ui/CopyLink.vue'

/**
 * What is currently filtering the grid, as removable chips, plus the share
 * link of that exact state. Rendered only while something is engaged.
 */
const props = defineProps<{
    schema: readonly FacetDefinition[]
    state: FacetState
    query: string
    shareUrl: string
    labels: { active: string; clear: string; copy: string; copied: string; copyError: string }
}>()

const emit = defineEmits<{
    clearFacet: [key: string]
    clearQuery: []
    clearAll: []
}>()

interface ActiveChip {
    key: string
    text: string
}

const chips = computed<ActiveChip[]>(() => {
    const chips: ActiveChip[] = []
    for (const facet of props.schema) {
        const text = describe(facet, props.state)
        if (text !== null) chips.push({ key: facet.key, text })
    }
    return chips
})

function describe(facet: FacetDefinition, state: FacetState): string | null {
    const selection = state[facet.key]
    if (selection === undefined) return null
    if (selection === true) return facet.label
    if (isRangeSelection(selection)) {
        const unit = facet.unit ? ` ${facet.unit}` : ''
        return `${facet.label} ${selection.min}–${selection.max}${unit}`
    }
    if (!isChoiceSelection(selection)) return null
    const labels = new Map(facet.options.map((option) => [option.value, option.label]))
    const joiner = selection.all ? ' + ' : ', '
    return `${facet.label}: ${selection.values.map((value) => labels.get(value) ?? value).join(joiner)}`
}
</script>

<template>
    <div class="active-filters" role="region" :aria-label="labels.active">
        <span class="facet__label">{{ labels.active }}</span>
        <button v-if="query.trim() !== ''" type="button" class="active-filters__chip"
                @click="emit('clearQuery')">
            <span>“{{ query.trim() }}”</span><i aria-hidden="true">×</i>
        </button>
        <button v-for="chip in chips" :key="chip.key" type="button" class="active-filters__chip"
                @click="emit('clearFacet', chip.key)">
            <span>{{ chip.text }}</span><i aria-hidden="true">×</i>
        </button>
        <button type="button" class="filter-clear" @click="emit('clearAll')">{{ labels.clear }}</button>
        <CopyLink class="active-filters__share" :url="shareUrl"
                  :labels="{ copy: labels.copy, copied: labels.copied, error: labels.copyError }" />
    </div>
</template>
