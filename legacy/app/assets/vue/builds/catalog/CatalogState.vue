<script setup lang="ts">
import type { UiLabels } from '../editor/editorLabels'

/**
 * Loading / error / ready tri-state shared by the three editor catalog sections
 * (champions, runes, items). They render the exact same hint and error card, so
 * the markup lives here once and each section only declares which state it is
 * in; the ready branch is the default slot, owned by the caller.
 */
defineProps<{
    isLoading: boolean
    hasError: boolean
    ui: UiLabels
}>()

const emit = defineEmits<{ retry: [] }>()
</script>

<template>
    <p v-if="isLoading" class="forge-hint" role="status">{{ ui.loading }}</p>
    <div v-else-if="hasError" class="forge-error">
        {{ ui.error }}
        <button type="button" class="hx-btn-ghost forge-btn-sm ml-3" @click="emit('retry')">
            {{ ui.retry }}
        </button>
    </div>
    <slot v-else />
</template>
