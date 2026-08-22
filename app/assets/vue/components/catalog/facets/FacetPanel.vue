<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
    countEngaged,
    groupFacets,
    isChoiceSelection,
    isGroupOpenByDefault,
    isRangeSelection,
    type FacetDefinition,
    type FacetGroup,
    type FacetState,
} from '../../../filter/facets'
import type { FacetCounts } from '../../../filter/facetCounts'
import type { FacetUniverse } from '../../../filter/useCardGrid'
import FacetChoice from './FacetChoice.vue'
import FacetRange from './FacetRange.vue'
import FacetToggle from './FacetToggle.vue'

/**
 * The facets of a list as an accordion of groups, each heading carrying how
 * many of its facets are engaged. A group unfolds on its own for the primary
 * axes and for anything engaged (a shared link, a chip); once unfolded it
 * stays so — clearing the last facet of a group must not fold it under the
 * pointer. The reader's own folding is remembered for the life of the panel
 * (the rail and the sheet fold independently). Presentation only: the facet
 * controls mutate the provided store themselves.
 */
const props = defineProps<{
    facets: readonly FacetDefinition[]
    state: FacetState
    universe: FacetUniverse
    counts: FacetCounts
    labels: { matchMode: string; matchAny: string; matchAll: string; min: string; max: string; clear: string }
}>()

const EMPTY_SET: ReadonlySet<string> = new Set()
const EMPTY_COUNTS: ReadonlyMap<string, number> = new Map()

/** The groups the reader folded or unfolded, and those that unfolded on their own. */
const folded = ref<Record<string, boolean>>({})

const groups = computed(() =>
    groupFacets(props.facets).map((group) => ({
        ...group,
        engaged: countEngaged(group.facets, props.state),
        isOpen: folded.value[group.name] ?? isGroupOpenByDefault(group, props.state),
    })),
)

// An engagement pins its group open, so the default never folds it back.
watch(
    () => props.state,
    (state) => {
        const pinned = groupFacets(props.facets)
            .filter((group) => folded.value[group.name] === undefined && countEngaged(group.facets, state) > 0)
            .map((group) => group.name)
        if (pinned.length) {
            folded.value = { ...folded.value, ...Object.fromEntries(pinned.map((name) => [name, true])) }
        }
    },
    { immediate: true },
)

function toggleGroup(group: FacetGroup, isOpen: boolean): void {
    folded.value = { ...folded.value, [group.name]: !isOpen }
}

function choiceSelection(key: string) {
    const selection = props.state[key]
    return selection !== undefined && isChoiceSelection(selection) ? selection : undefined
}

function rangeSelection(key: string) {
    const selection = props.state[key]
    return selection !== undefined && isRangeSelection(selection) ? selection : undefined
}
</script>

<template>
    <div class="facet-panel">
        <section v-for="group in groups" :key="group.name" class="facet-group"
                 :class="{ 'facet-group--engaged': group.engaged > 0 }">
            <button type="button" class="facet-group__title" :aria-expanded="group.isOpen"
                    @click="toggleGroup(group, group.isOpen)">
                <span class="filter-marker" aria-hidden="true"></span>
                <span class="facet-group__name">{{ group.name }}</span>
                <b v-if="group.engaged" class="facet-group__badge">{{ group.engaged }}</b>
                <svg class="facet-group__chevron" viewBox="0 0 20 20" fill="currentColor"
                     aria-hidden="true">
                    <path fill-rule="evenodd" clip-rule="evenodd"
                          d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06
                             1.06l-4.24 4.24a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z" />
                </svg>
            </button>
            <div v-if="group.isOpen" class="facet-group__body">
                <template v-for="facet in group.facets" :key="facet.key">
                    <FacetChoice v-if="facet.kind === 'choice'" :facet="facet"
                                 :selection="choiceSelection(facet.key)"
                                 :present="universe.present[facet.key] ?? EMPTY_SET"
                                 :counts="counts.options[facet.key] ?? EMPTY_COUNTS"
                                 :labels="labels" />
                    <FacetRange v-else-if="facet.kind === 'range' && universe.bounds[facet.key]"
                                :facet="facet" :selection="rangeSelection(facet.key)"
                                :bounds="universe.bounds[facet.key]" :labels="labels" />
                    <FacetToggle v-else-if="facet.kind === 'toggle'" :facet="facet"
                                 :is-on="state[facet.key] === true"
                                 :count="counts.flagged[facet.key] ?? 0" />
                </template>
            </div>
        </section>
    </div>
</template>
