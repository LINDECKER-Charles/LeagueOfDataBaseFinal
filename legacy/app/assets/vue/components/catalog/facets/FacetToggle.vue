<script setup lang="ts">
import type { FacetDefinition } from '../../../filter/facets'
import { injectFacetStore } from '../../../filter/useFacetState'

/** One flag facet as a switch ("purchasable only") with the count it keeps. */
defineProps<{
    facet: FacetDefinition
    isOn: boolean
    /** How many cards in the current context carry the flag (facetCounts). */
    count: number
}>()

const store = injectFacetStore()
</script>

<template>
    <label class="facet facet--toggle" :class="{ 'facet--toggle-on': isOn }">
        <input type="checkbox" class="hx-switch" :checked="isOn"
               @change="store.setToggle(facet.key, ($event.target as HTMLInputElement).checked)">
        <span class="facet__toggle-label">{{ facet.label }}</span>
        <span class="facet__count">{{ count }}</span>
    </label>
</template>
