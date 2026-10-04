<script setup lang="ts">
import { computed } from 'vue'
import type { ChoiceSelection, FacetDefinition } from '../../../filter/facets'
import { injectFacetStore } from '../../../filter/useFacetState'

/**
 * One choice facet as a row of counted chips — only the values the grid
 * actually carries, in the schema's order (unknown tokens last). A value no
 * card in the current context carries is shown but disabled, so the reader
 * sees the axis without chasing an empty result. Multi-select ORs the
 * values; the optional any/all switch turns that into an AND.
 */
const props = defineProps<{
    facet: FacetDefinition
    selection?: ChoiceSelection
    /** Tokens present in the grid (from useCardGrid's universe). */
    present: ReadonlySet<string>
    /** Per-value result counts in the current context (facetCounts). */
    counts: ReadonlyMap<string, number>
    labels: { matchMode: string; matchAny: string; matchAll: string }
}>()

const store = injectFacetStore()

const selected = computed(() => new Set(props.selection?.values ?? []))
/* Offered with two values, and kept while match-all is on: in that mode a
   chip no selected card shares is disabled, so the way back must stay. */
const showsMatchMode = computed(
    () => props.facet.matchAll && (selected.value.size > 1 || props.selection?.all === true),
)

const options = computed(() => {
    const known = props.facet.options.filter((option) => props.present.has(option.value))
    const knownValues = new Set(known.map((option) => option.value))
    const extra = [...props.present]
        .filter((value) => !knownValues.has(value))
        .sort()
        .map((value) => ({ value, label: value }))
    return [...known, ...extra].map((option) => {
        const isOn = selected.value.has(option.value)
        const count = props.counts.get(option.value) ?? 0
        return { ...option, isOn, count, isDisabled: count === 0 && !isOn }
    })
})
</script>

<template>
    <div class="facet" role="group" :aria-label="facet.label">
        <span class="facet__label">{{ facet.label }}</span>
        <div class="facet__chips">
            <button v-for="option in options" :key="option.value" type="button"
                    class="filter-chip" :class="{ 'filter-chip--on': option.isOn }"
                    :aria-pressed="option.isOn" :disabled="option.isDisabled"
                    @click="store.toggleChoice(facet, option.value)">
                {{ option.label }}
                <span class="filter-chip__count">{{ option.count }}</span>
            </button>
        </div>
        <div v-if="showsMatchMode" class="facet__mode-row">
            <span class="facet__hint">{{ labels.matchMode }}</span>
            <span class="facet__mode" role="group" :aria-label="labels.matchMode">
                <button type="button" class="facet__mode-btn"
                        :class="{ 'facet__mode-btn--on': !selection?.all }"
                        :aria-pressed="!selection?.all"
                        @click="store.setMatchAll(facet.key, false)">{{ labels.matchAny }}</button>
                <button type="button" class="facet__mode-btn"
                        :class="{ 'facet__mode-btn--on': selection?.all }"
                        :aria-pressed="selection?.all === true"
                        @click="store.setMatchAll(facet.key, true)">{{ labels.matchAll }}</button>
            </span>
        </div>
    </div>
</template>
