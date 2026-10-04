<script setup lang="ts">
import { computed } from 'vue'
import { formatTemplate, pluralTemplate, splitTemplate } from '../../../i18n/formatTemplate'

/**
 * The head of the results column: how many cards match and where the reader
 * stands in them, the page size segments, the pager. Presentation only.
 */
const props = defineProps<{
    matching: number
    page: number
    pageCount: number
    size: number
    sizeOptions: readonly { value: number; label: string }[]
    labels: { results: string; resultsOne: string; page: string; perPage: string; prev: string; next: string }
}>()

const emit = defineEmits<{
    size: [value: number]
    move: [delta: number]
}>()

const resultText = computed(() =>
    splitTemplate(pluralTemplate(props.matching, props.labels.resultsOne, props.labels.results), 'count'),
)
const pageText = computed(() =>
    formatTemplate(props.labels.page, { page: props.page, count: props.pageCount }),
)
</script>

<template>
    <div class="filter-toolbar">
        <p class="filter-toolbar__count" aria-live="polite">
            <span class="filter-toolbar__count-text">{{ resultText.before }}</span>
            <span class="filter-toolbar__count-n">{{ matching }}</span>
            <span class="filter-toolbar__count-text">{{ resultText.after }}</span>
            <span class="filter-toolbar__dot" aria-hidden="true">·</span>
            <span class="filter-toolbar__page">{{ pageText }}</span>
        </p>
        <div class="filter-toolbar__tools">
            <div class="filter-toolbar__sizes">
                <span class="facet__label">{{ labels.perPage }}</span>
                <div class="filter-segments" role="group" :aria-label="labels.perPage">
                    <button v-for="option in sizeOptions" :key="option.value" type="button"
                            class="pp-btn" :class="{ 'pp-btn--on': size === option.value }"
                            :aria-pressed="size === option.value"
                            @click="emit('size', option.value)">{{ option.label }}</button>
                </div>
            </div>
            <div v-if="pageCount > 1" class="filter-pager">
                <button type="button" class="filter-nav" :aria-label="labels.prev"
                        :disabled="page === 1" @click="emit('move', -1)">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                         aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg>
                </button>
                <button type="button" class="filter-nav" :aria-label="labels.next"
                        :disabled="page === pageCount" @click="emit('move', 1)">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                         aria-hidden="true"><path d="M9 6l6 6-6 6" /></svg>
                </button>
            </div>
        </div>
    </div>
</template>
