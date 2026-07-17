<script setup lang="ts">
import type { ItemOption } from './catalogTypes'
import ItemSearch from './ItemSearch.vue'
import { MAX_ITEMS_PER_STEP, MAX_STEPS, stepGold } from './stepList'
import type { BuildStep } from './structure'
import { formatCounter, type StepsLabels, type UiLabels } from './useBuildEditor'

/**
 * Purchase-order section: vertical list of steps (label + optional note +
 * item medallions), reordered with real buttons — deliberately no drag&drop,
 * keyboard/screen-reader users get the same power. Item ids unknown on this
 * patch render as ghost medallions.
 */
const props = defineProps<{
    steps: BuildStep[]
    itemsById: Map<string, ItemOption>
    options: ItemOption[] | null
    isLoading: boolean
    hasError: boolean
    canAddStepNow: boolean
    canAddItemTo: (stepIndex: number) => boolean
    labels: StepsLabels
    ui: UiLabels
}>()

const emit = defineEmits<{
    addStep: []
    removeStep: [index: number]
    moveStep: [index: number, delta: number]
    editStep: [index: number, patch: Partial<Pick<BuildStep, 'label' | 'note'>>]
    addItem: [stepIndex: number, itemId: string]
    removeItem: [stepIndex: number, itemIndex: number]
    moveItem: [stepIndex: number, itemIndex: number, delta: number]
    retry: []
}>()

function onLabelInput(index: number, event: Event): void {
    emit('editStep', index, { label: (event.target as HTMLInputElement).value })
}

function onNoteInput(index: number, event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value
    emit('editStep', index, { note: value.trim() === '' ? null : value })
}

function goldOf(itemId: string): number | null {
    return props.itemsById.get(itemId)?.gold ?? null
}

function itemName(itemId: string): string {
    return props.itemsById.get(itemId)?.name ?? itemId
}

function itemImage(itemId: string): string | null {
    return props.itemsById.get(itemId)?.image ?? null
}

/** Ghost only once the catalog answered — an id absent from it no longer exists. */
function isGhostItem(itemId: string): boolean {
    return props.options !== null && !props.itemsById.has(itemId)
}
</script>

<template>
    <div class="space-y-5">
        <datalist id="forge-step-presets">
            <option v-for="preset in labels.presets" :key="preset" :value="preset" />
        </datalist>

        <article v-for="(step, index) in steps" :key="index" class="forge-step">
            <div class="flex flex-wrap items-center gap-2">
                <span class="forge-hint">{{ index + 1 }}.</span>
                <input
                    type="text"
                    class="hx-input forge-step__label flex-1"
                    maxlength="40"
                    list="forge-step-presets"
                    :value="step.label"
                    :placeholder="labels.label"
                    :aria-label="labels.label"
                    @input="onLabelInput(index, $event)"
                />
                <span class="hx-chip" :title="labels.gold">{{ stepGold(step, goldOf) }} ◆</span>
                <span class="ml-auto flex items-center gap-1.5">
                    <button type="button" class="forge-icon-btn" :disabled="index === 0"
                            :aria-label="labels.moveUp" :title="labels.moveUp"
                            @click="emit('moveStep', index, -1)">↑</button>
                    <button type="button" class="forge-icon-btn" :disabled="index === steps.length - 1"
                            :aria-label="labels.moveDown" :title="labels.moveDown"
                            @click="emit('moveStep', index, 1)">↓</button>
                    <button type="button" class="forge-icon-btn forge-btn-danger" :disabled="steps.length === 1"
                            :aria-label="labels.remove" :title="labels.remove"
                            @click="emit('removeStep', index)">×</button>
                </span>
            </div>

            <textarea
                rows="2"
                class="hx-input mt-3"
                maxlength="300"
                :value="step.note ?? ''"
                :placeholder="labels.note"
                :aria-label="labels.note"
                @input="onNoteInput(index, $event)"
            ></textarea>

            <ul v-if="step.items.length" class="mt-3 flex flex-wrap gap-3">
                <li v-for="(itemId, itemIndex) in step.items" :key="`${itemIndex}-${itemId}`" class="text-center">
                    <span
                        class="forge-item block"
                        :class="{ 'forge-ghost': isGhostItem(itemId) }"
                        :title="isGhostItem(itemId) ? `${itemId} — ${ui.ghost}` : itemName(itemId)"
                    >
                        <img v-if="itemImage(itemId)" :src="itemImage(itemId) ?? undefined" :alt="itemName(itemId)"
                             loading="lazy" decoding="async" />
                        <span v-else class="forge-item__fallback">{{ itemId }}</span>
                    </span>
                    <span class="mt-1 flex justify-center gap-1">
                        <button type="button" class="forge-icon-btn" :disabled="itemIndex === 0"
                                :aria-label="labels.moveUp" :title="labels.moveUp"
                                @click="emit('moveItem', index, itemIndex, -1)">‹</button>
                        <button type="button" class="forge-icon-btn"
                                :disabled="itemIndex === step.items.length - 1"
                                :aria-label="labels.moveDown" :title="labels.moveDown"
                                @click="emit('moveItem', index, itemIndex, 1)">›</button>
                        <button type="button" class="forge-icon-btn forge-btn-danger"
                                :aria-label="labels.removeItem" :title="labels.removeItem"
                                @click="emit('removeItem', index, itemIndex)">×</button>
                    </span>
                </li>
            </ul>

            <p class="forge-hint mt-3">
                {{ formatCounter(ui.counter, step.items.length, MAX_ITEMS_PER_STEP) }}
            </p>
            <ItemSearch
                class="mt-2"
                :options="options"
                :is-loading="isLoading"
                :has-error="hasError"
                :can-add="canAddItemTo(index)"
                :labels="labels"
                :ui="ui"
                @add="(itemId) => emit('addItem', index, itemId)"
                @retry="emit('retry')"
            />
        </article>

        <div class="flex items-center justify-between gap-3">
            <button type="button" class="hx-btn-ghost" :disabled="!canAddStepNow" @click="emit('addStep')">
                {{ labels.add }}
            </button>
            <span class="forge-hint">{{ formatCounter(ui.counter, steps.length, MAX_STEPS) }}</span>
        </div>
    </div>
</template>
