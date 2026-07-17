<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ChampionOption } from './catalogTypes'
import type { ChampionLabels, UiLabels } from './useBuildEditor'

/**
 * Champion section: live search over the picker catalog + portrait grid.
 * Selection state lives in the parent; a selected id absent from the current
 * catalog renders as an explicit ghost (never silently dropped).
 */
const props = defineProps<{
    options: ChampionOption[] | null
    isLoading: boolean
    hasError: boolean
    selectedId: string
    labels: ChampionLabels
    ui: UiLabels
}>()

const emit = defineEmits<{ select: [id: string]; retry: [] }>()

const query = ref('')

const filtered = computed<ChampionOption[]>(() => {
    const all = props.options ?? []
    const q = query.value.trim().toLowerCase()
    if (!q) return all
    return all.filter((c) => c.name.toLowerCase().includes(q) || c.id.toLowerCase().includes(q))
})

const selected = computed(() => (props.options ?? []).find((c) => c.id === props.selectedId) ?? null)
const isGhostSelection = computed(
    () => props.selectedId !== '' && props.options !== null && selected.value === null,
)
</script>

<template>
    <div class="space-y-4">
        <p v-if="selectedId" class="flex items-center gap-2.5">
            <span class="forge-hint">{{ labels.selected }}</span>
            <span class="font-beaufort text-lg uppercase tracking-wide text-gold-bright">
                {{ selected?.name ?? selectedId }}
            </span>
            <span v-if="isGhostSelection" class="hx-chip forge-ghost">{{ ui.ghost }}</span>
        </p>

        <input
            v-model="query"
            type="search"
            class="hx-input"
            :placeholder="labels.search"
            :aria-label="labels.search"
        />

        <p v-if="isLoading" class="forge-hint" role="status">{{ ui.loading }}</p>
        <div v-else-if="hasError" class="forge-error">
            {{ ui.error }}
            <button type="button" class="hx-btn-ghost forge-btn-sm ml-3" @click="emit('retry')">{{ ui.retry }}</button>
        </div>

        <template v-else-if="options">
            <div v-if="filtered.length" class="forge-champs" role="listbox" :aria-label="labels.title">
                <button
                    v-for="champion in filtered"
                    :key="champion.id"
                    type="button"
                    class="forge-champ"
                    :class="{ 'forge-champ--on': champion.id === selectedId }"
                    role="option"
                    :aria-selected="champion.id === selectedId"
                    :title="champion.name"
                    @click="emit('select', champion.id)"
                >
                    <img v-if="champion.image" :src="champion.image" :alt="''" loading="lazy" decoding="async" />
                    <span class="forge-champ__name">{{ champion.name }}</span>
                </button>
            </div>
            <p v-else class="forge-hint">{{ labels.empty }}</p>
        </template>
    </div>
</template>
