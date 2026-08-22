<script setup lang="ts">
import { computed } from 'vue'
import type { ChoiceSelection, FacetDefinition } from '../../../filter/facets'

/**
 * One choice facet as a row of chips — only the values the grid actually
 * carries, in the schema's order (unknown tokens last). Multi-select ORs the
 * values; the optional any/all switch turns that into an AND.
 */
const props = defineProps<{
    facet: FacetDefinition
    selection?: ChoiceSelection
    /** Tokens present in the grid (from useCardGrid's universe). */
    present: ReadonlySet<string>
    labels: { matchAny: string; matchAll: string }
    /** Inline variant (label left, chips right) for the primary row. */
    inline?: boolean
}>()

const emit = defineEmits<{
    toggle: [value: string]
    matchAll: [all: boolean]
}>()

const options = computed(() => {
    const known = props.facet.options.filter((option) => props.present.has(option.value))
    const knownValues = new Set(known.map((option) => option.value))
    const extra = [...props.present]
        .filter((value) => !knownValues.has(value))
        .sort()
        .map((value) => ({ value, label: value }))
    return [...known, ...extra]
})

const selected = computed(() => new Set(props.selection?.values ?? []))
const showsMatchMode = computed(() => props.facet.matchAll && selected.value.size > 1)
</script>

<template>
    <div class="facet" :class="{ 'facet--inline': inline }" role="group" :aria-label="facet.label">
        <span class="facet__label">{{ facet.label }}</span>
        <div class="facet__chips">
            <button v-for="option in options" :key="option.value" type="button"
                    class="filter-chip" :class="{ 'filter-chip--on': selected.has(option.value) }"
                    :aria-pressed="selected.has(option.value)"
                    @click="emit('toggle', option.value)">{{ option.label }}</button>
            <span v-if="showsMatchMode" class="facet__mode" role="group" :aria-label="facet.label">
                <button type="button" class="facet__mode-btn"
                        :class="{ 'facet__mode-btn--on': !selection?.all }"
                        :aria-pressed="!selection?.all"
                        @click="emit('matchAll', false)">{{ labels.matchAny }}</button>
                <button type="button" class="facet__mode-btn"
                        :class="{ 'facet__mode-btn--on': selection?.all }"
                        :aria-pressed="selection?.all === true"
                        @click="emit('matchAll', true)">{{ labels.matchAll }}</button>
            </span>
        </div>
    </div>
</template>
