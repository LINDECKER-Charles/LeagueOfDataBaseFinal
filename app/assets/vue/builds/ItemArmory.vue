<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import type { ItemOption } from './catalogTypes'
import { formatCounter, formatTemplate, type ArmoryLabels, type UiLabels } from './editorLabels'
import { ITEM_CATEGORY_ALL, ITEM_CATEGORY_KEYS, matchesCategory, type ItemCategoryKey } from './itemCategories'

/**
 * Item armory — a native <dialog> browse-and-add picker opened per step. Unlike
 * a type-ahead, the FULL catalog is scrollable from the start; a search box and
 * category chips narrow it. Multi-add: a click adds the item and the modal STAYS
 * open (running counter + per-tile "already in step" badge), so a whole step is
 * composed in one pass. The <dialog> gives us the top layer, an inert
 * background, focus trapping and Escape/Android-back dismissal for free.
 */
interface ArmoryStep {
    index: number
    label: string
    items: string[]
}

const props = defineProps<{
    open: boolean
    step: ArmoryStep | null
    options: ItemOption[] | null
    isLoading: boolean
    hasError: boolean
    canAdd: boolean
    maxItems: number
    labels: ArmoryLabels
    ui: UiLabels
}>()

const emit = defineEmits<{ add: [itemId: string]; close: []; retry: [] }>()

const dialog = ref<HTMLDialogElement | null>(null)
const query = ref('')
const activeCategory = ref<ItemCategoryKey>(ITEM_CATEGORY_ALL)
// Adds made since this open — pure UI feedback, reset each time the modal opens.
const addedCount = ref(0)

const categoryChips = computed(() =>
    ITEM_CATEGORY_KEYS.map((key) => ({ key, label: props.labels.categories[key] })),
)

const results = computed<ItemOption[]>(() => {
    const q = query.value.trim().toLowerCase()
    return (props.options ?? []).filter(
        (item) => matchesCategory(item, activeCategory.value) && (q === '' || item.name.toLowerCase().includes(q)),
    )
})

const targetTitle = computed(() => {
    const step = props.step
    if (!step) return ''
    return step.label.trim() === '' ? `${step.index + 1}` : `${step.index + 1}. ${step.label}`
})

const placedCount = computed(() => props.step?.items.length ?? 0)
const addedLabel = computed(() => formatTemplate(props.labels.added, { count: addedCount.value }))

/** How many copies of an item already sit in the target step (duplicates allowed). */
function countInStep(itemId: string): number {
    return props.step?.items.filter((id) => id === itemId).length ?? 0
}

function onAdd(itemId: string): void {
    if (!props.canAdd) return
    emit('add', itemId)
    addedCount.value += 1
}

function setCategory(key: ItemCategoryKey): void {
    activeCategory.value = key
}

// Keep the native dialog in sync with the parent-owned `open` flag. showModal()
// on an already-open dialog throws, hence the .open guards.
watch(
    () => props.open,
    (open) => {
        const el = dialog.value
        if (!el) return
        if (open && !el.open) {
            addedCount.value = 0
            query.value = ''
            activeCategory.value = ITEM_CATEGORY_ALL
            el.showModal()
        } else if (!open && el.open) {
            el.close()
        }
    },
)

onMounted(() => {
    if (props.open) dialog.value?.showModal()
})

/** Escape / backdrop close fires the native `close`; relay it once to the parent. */
function onNativeClose(): void {
    if (props.open) emit('close')
}

function onBackdropClick(event: MouseEvent): void {
    if (event.target === dialog.value) emit('close')
}
</script>

<template>
    <dialog
        ref="dialog"
        class="armory"
        :aria-label="labels.title"
        @close="onNativeClose"
        @click="onBackdropClick"
    >
        <header class="armory__head">
            <div class="min-w-0">
                <p class="eyebrow">{{ labels.title }}</p>
                <p v-if="step" class="armory__target">{{ targetTitle }}</p>
            </div>
            <button type="button" class="armory__close" :aria-label="labels.close" @click="emit('close')">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                    <path d="M18 6L6 18M6 6l12 12" />
                </svg>
            </button>
        </header>

        <div class="armory__tools">
            <label class="armory__search">
                <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true">
                    <circle cx="9" cy="9" r="6" /><path d="M14 14l4 4" stroke-linecap="round" />
                </svg>
                <input
                    v-model="query"
                    type="search"
                    :placeholder="labels.search"
                    :aria-label="labels.search"
                />
            </label>
            <div class="armory__cats" role="group" :aria-label="labels.title">
                <button
                    v-for="chip in categoryChips"
                    :key="chip.key"
                    type="button"
                    class="armory__cat"
                    :class="{ 'armory__cat--on': activeCategory === chip.key }"
                    :aria-pressed="activeCategory === chip.key"
                    @click="setCategory(chip.key)"
                >
                    {{ chip.label }}
                </button>
            </div>
        </div>

        <div class="armory__body">
            <p v-if="isLoading" class="forge-hint" role="status">{{ ui.loading }}</p>
            <div v-else-if="hasError" class="forge-error">
                {{ ui.error }}
                <button type="button" class="hx-btn-ghost forge-btn-sm ml-3" @click="emit('retry')">{{ ui.retry }}</button>
            </div>
            <template v-else>
                <div v-if="results.length" class="armory__grid">
                    <button
                        v-for="item in results"
                        :key="item.id"
                        type="button"
                        class="armory-item"
                        :class="{ 'armory-item--placed': countInStep(item.id) > 0 }"
                        :disabled="!canAdd"
                        :title="item.name"
                        @click="onAdd(item.id)"
                    >
                        <span class="armory-item__icon">
                            <img v-if="item.image" :src="item.image" alt="" loading="lazy" decoding="async" />
                            <span
                                v-if="countInStep(item.id) > 0"
                                class="armory-item__badge"
                                :title="formatTemplate(labels.inStep, { count: countInStep(item.id) })"
                                >{{ countInStep(item.id) }}</span
                            >
                        </span>
                        <span class="armory-item__name">{{ item.name }}</span>
                        <span class="armory-item__gold">{{ item.gold }} ◆</span>
                    </button>
                </div>
                <p v-else class="forge-hint py-6 text-center">{{ labels.empty }}</p>
            </template>
        </div>

        <footer class="armory__foot">
            <span class="armory__count" :class="{ 'armory__count--full': !canAdd }">
                <template v-if="addedCount > 0">{{ addedLabel }} · </template>
                {{ formatCounter(ui.counter, placedCount, maxItems) }}
                <template v-if="!canAdd"> — {{ labels.full }}</template>
            </span>
            <button type="button" class="armory__done" @click="emit('close')">{{ labels.done }}</button>
        </footer>
    </dialog>
</template>
