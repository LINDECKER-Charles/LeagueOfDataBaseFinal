<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ChampionOption } from './catalogTypes'
import type { ChampionLabels, UiLabels } from './editorLabels'

/**
 * Champion section: a collapsible snippet header (chosen champion portrait +
 * open/close toggle) over a live-searchable portrait grid. Selection state
 * lives in the parent; a selected id absent from the current catalog renders
 * as an explicit ghost (never silently dropped).
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
// Start expanded when nothing is chosen yet (the user must pick); collapse to
// the snippet when editing an existing build.
const isOpen = ref(props.selectedId === '')

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

/** Picking collapses the grid back to the snippet — the choice is made. */
function choose(id: string): void {
    emit('select', id)
    isOpen.value = false
}
</script>

<template>
    <div class="space-y-4">
        <button
            type="button"
            class="forge-champ-toggle"
            :aria-expanded="isOpen"
            aria-controls="forge-champ-list"
            @click="isOpen = !isOpen"
        >
            <span class="forge-champ-toggle__id">
                <img
                    v-if="selected?.image"
                    :src="selected.image"
                    :alt="''"
                    class="forge-selected"
                    decoding="async"
                />
                <span class="forge-champ-toggle__label">
                    <span class="forge-hint">{{ selectedId ? labels.selected : labels.title }}</span>
                    <span v-if="selectedId" class="font-beaufort text-base uppercase tracking-wide text-gold-bright">
                        {{ selected?.name ?? selectedId }}
                    </span>
                </span>
                <span v-if="isGhostSelection" class="hx-chip forge-ghost">{{ ui.ghost }}</span>
            </span>
            <span class="forge-champ-toggle__action">
                {{ isOpen ? labels.close : labels.open }}
                <svg class="forge-champ-toggle__chevron" viewBox="0 0 16 16" aria-hidden="true">
                    <path d="M4 6l4 4 4-4" fill="none" stroke="currentColor" stroke-width="1.5" />
                </svg>
            </span>
        </button>

        <div v-show="isOpen" id="forge-champ-list" class="space-y-4">
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
                <button type="button" class="hx-btn-ghost forge-btn-sm ml-3" @click="emit('retry')">
                    {{ ui.retry }}
                </button>
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
                        @click="choose(champion.id)"
                    >
                        <img v-if="champion.image" :src="champion.image" :alt="''" loading="lazy" decoding="async" />
                        <span class="forge-champ__name">{{ champion.name }}</span>
                    </button>
                </div>
                <p v-else class="forge-hint">{{ labels.empty }}</p>
            </template>
        </div>
    </div>
</template>
