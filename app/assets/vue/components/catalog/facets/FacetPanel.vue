<script setup lang="ts">
import { computed } from 'vue'
import {
    isChoiceSelection,
    isRangeSelection,
    type FacetDefinition,
    type FacetState,
    type RangeSelection,
} from '../../../filter/facets'
import type { FacetUniverse } from '../../../filter/useCardGrid'
import FacetChoice from './FacetChoice.vue'
import FacetRange from './FacetRange.vue'
import FacetToggle from './FacetToggle.vue'

/**
 * The facets of a list grouped under their headings, each drawn by its kind.
 * Presentation only: every interaction is re-emitted to the island, which
 * owns the state. Groups can start collapsed (the mobile sheet) so a dozen
 * sliders do not bury the primary chips.
 */
const props = defineProps<{
    facets: readonly FacetDefinition[]
    state: FacetState
    universe: FacetUniverse
    labels: { matchAny: string; matchAll: string; min: string; max: string; clear: string }
    collapsed?: boolean
}>()

const emit = defineEmits<{
    toggleChoice: [facet: FacetDefinition, value: string]
    matchAll: [key: string, all: boolean]
    range: [key: string, range: RangeSelection | null]
    toggle: [key: string, isOn: boolean]
}>()

const groups = computed(() => {
    const byGroup = new Map<string, FacetDefinition[]>()
    for (const facet of props.facets) {
        byGroup.set(facet.group, [...(byGroup.get(facet.group) ?? []), facet])
    }
    return [...byGroup].map(([name, facets]) => ({ name, facets }))
})

function choiceSelection(key: string) {
    const selection = props.state[key]
    return selection !== undefined && isChoiceSelection(selection) ? selection : undefined
}

function rangeSelection(key: string) {
    const selection = props.state[key]
    return selection !== undefined && isRangeSelection(selection) ? selection : undefined
}

function groupHasSelection(facets: readonly FacetDefinition[]): boolean {
    return facets.some((facet) => props.state[facet.key] !== undefined)
}
</script>

<template>
    <div class="facet-panel">
        <details v-for="group in groups" :key="group.name" class="facet-group"
                 :open="!collapsed || groupHasSelection(group.facets)">
            <summary class="facet-group__title">{{ group.name }}</summary>
            <div class="facet-group__body">
                <template v-for="facet in group.facets" :key="facet.key">
                    <FacetChoice v-if="facet.kind === 'choice'" :facet="facet"
                                 :selection="choiceSelection(facet.key)"
                                 :present="universe.present[facet.key] ?? new Set()"
                                 :labels="labels"
                                 @toggle="(value) => emit('toggleChoice', facet, value)"
                                 @match-all="(all) => emit('matchAll', facet.key, all)" />
                    <FacetRange v-else-if="facet.kind === 'range' && universe.bounds[facet.key]"
                                :facet="facet" :selection="rangeSelection(facet.key)"
                                :bounds="universe.bounds[facet.key]" :labels="labels"
                                @change="(range) => emit('range', facet.key, range)" />
                    <FacetToggle v-else-if="facet.kind === 'toggle'" :facet="facet"
                                 :is-on="state[facet.key] === true"
                                 :count="universe.flagged[facet.key] ?? 0"
                                 @change="(isOn) => emit('toggle', facet.key, isOn)" />
                </template>
            </div>
        </details>
    </div>
</template>
