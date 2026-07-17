<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ItemOption } from './catalogTypes'
import type { StepsLabels, UiLabels } from './useBuildEditor'

/**
 * Per-step item finder: type-ahead over the picker catalog, one click adds the
 * item to the owning step. Results only appear once a query is typed (keeps
 * ten steps' worth of DOM light).
 */
const MAX_RESULTS = 24

const props = defineProps<{
    options: ItemOption[] | null
    isLoading: boolean
    hasError: boolean
    canAdd: boolean
    labels: StepsLabels
    ui: UiLabels
}>()

const emit = defineEmits<{ add: [itemId: string]; retry: [] }>()

const query = ref('')

const results = computed<ItemOption[]>(() => {
    const q = query.value.trim().toLowerCase()
    if (!q) return []
    return (props.options ?? []).filter((item) => item.name.toLowerCase().includes(q)).slice(0, MAX_RESULTS)
})
</script>

<template>
    <div>
        <input
            v-model="query"
            type="search"
            class="hx-input"
            :placeholder="labels.searchItem"
            :aria-label="labels.searchItem"
        />

        <p v-if="isLoading" class="forge-hint mt-2" role="status">{{ ui.loading }}</p>
        <div v-else-if="hasError" class="forge-error mt-2">
            {{ ui.error }}
            <button type="button" class="hx-btn-ghost forge-btn-sm ml-3" @click="emit('retry')">{{ ui.retry }}</button>
        </div>

        <template v-else-if="query.trim()">
            <div v-if="results.length" class="forge-results">
                <button
                    v-for="item in results"
                    :key="item.id"
                    type="button"
                    class="forge-result"
                    :disabled="!canAdd"
                    :title="item.name"
                    @click="emit('add', item.id)"
                >
                    <img v-if="item.image" :src="item.image" alt="" loading="lazy" decoding="async" />
                    <span>{{ item.name }}</span>
                    <b>{{ item.gold }} ◆</b>
                </button>
            </div>
            <p v-else class="forge-hint mt-2">{{ labels.itemEmpty }}</p>
        </template>
    </div>
</template>
