<script setup lang="ts">
import type { FacetDefinition } from '../../../filter/facets'

/** One flag facet: a single pressed/unpressed chip ("purchasable only"). */
defineProps<{
    facet: FacetDefinition
    isOn: boolean
    /** How many cards carry the flag — shown so the reader knows what the switch keeps. */
    count: number
}>()

const emit = defineEmits<{ change: [isOn: boolean] }>()
</script>

<template>
    <div class="facet facet--toggle">
        <button type="button" class="filter-chip" :class="{ 'filter-chip--on': isOn }"
                :aria-pressed="isOn" @click="emit('change', !isOn)">
            {{ facet.label }}
            <span class="facet__count">{{ count }}</span>
        </button>
    </div>
</template>
