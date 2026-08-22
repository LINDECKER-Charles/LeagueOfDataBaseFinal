<script setup lang="ts">
import { ref } from 'vue'
import { SEARCH_SHORTCUT_KEY } from '../../../search/useSearchShortcut'

/**
 * The live search field of the filter, drawn once for the rail (with its
 * keyboard hint) and the mobile bar. The island keeps the query and reaches
 * the input through `field` for the `/` shortcut.
 */
defineProps<{
    placeholder: string
    /** Shows the `/` hint — the rail only; the bar has no keyboard. */
    withHint?: boolean
}>()

const query = defineModel<string>({ required: true })
const field = ref<HTMLInputElement | null>(null)

defineExpose({ field })
</script>

<template>
    <label class="filter-search">
        <svg class="filter-search__icon" viewBox="0 0 20 20" fill="none" stroke="currentColor"
             stroke-width="1.7" aria-hidden="true">
            <circle cx="9" cy="9" r="6" /><path d="M14 14l4 4" stroke-linecap="round" />
        </svg>
        <input ref="field" v-model="query" type="search" class="filter-search__input"
               :placeholder="placeholder" :aria-label="placeholder" />
        <kbd v-if="withHint" class="filter-search__kbd" aria-hidden="true">{{ SEARCH_SHORTCUT_KEY }}</kbd>
    </label>
</template>
