<script setup lang="ts">
import { computed, ref } from 'vue'
import type { ItemOption } from './catalogTypes'
import type { ArmoryLabels, DndLabels, StepsLabels, UiLabels } from './editorLabels'
import { formatCounter } from './editorLabels'
import ItemArmory from './ItemArmory.vue'
import { MAX_ITEMS_PER_STEP, MAX_STEPS, stepGold, type ItemLocation } from './stepList'
import type { BuildStep } from './structure'
import type { GhostReason } from './useBuildEditor'
import { useDragReorder } from './useDragReorder'

/**
 * Purchase-order section: vertical list of steps (label + optional note + item
 * medallions). Items are added through the {@see ItemArmory} modal (one "+ Add"
 * tile per step); reordering — steps, items, cross-step moves — stays drag-and-
 * drop as a PROGRESSIVE enhancement over the ↑↓/‹› buttons (drag handles hide on
 * coarse pointers). Ghost medallions distinguish "unknown on this patch" from
 * "excluded by the selected game mode".
 */
type DragSource = { kind: 'step'; index: number } | { kind: 'item'; step: number; index: number }

type DropTarget = { kind: 'step'; insert: number } | { kind: 'item'; step: number; insert: number }

const props = defineProps<{
    steps: BuildStep[]
    resolveItem: (itemId: string) => ItemOption | undefined
    ghostOf: (itemId: string) => GhostReason
    options: ItemOption[] | null
    isLoading: boolean
    hasError: boolean
    canAddStepNow: boolean
    canAddItemTo: (stepIndex: number) => boolean
    canReceiveItem: (stepIndex: number, from: ItemLocation) => boolean
    labels: StepsLabels
    armory: ArmoryLabels
    dnd: DndLabels
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
    reorderStep: [from: number, insert: number]
    moveItemTo: [from: ItemLocation, to: ItemLocation]
    dragCancelled: []
    retry: []
}>()

const drag = useDragReorder<DragSource, DropTarget>({
    onCommit: commitDrop,
    onCancel: () => emit('dragCancelled'),
})

// Which step the armory is composing (null = closed). A single modal instance
// serves every step; the pure step-list rules own the actual mutations.
const armoryStep = ref<number | null>(null)

const armoryStepInfo = computed(() => {
    const index = armoryStep.value
    if (index === null) return null
    const step = props.steps[index]
    return step ? { index, label: step.label, items: step.items } : null
})

function openArmory(index: number): void {
    armoryStep.value = index
}

function commitDrop(source: DragSource, target: DropTarget): void {
    if (source.kind === 'step' && target.kind === 'step') {
        emit('reorderStep', source.index, target.insert)
    } else if (source.kind === 'item' && target.kind === 'item') {
        emit('moveItemTo', { step: source.step, index: source.index }, { step: target.step, index: target.insert })
    }
}

/** Whether the active item drag may drop into this step (capacity rules). */
function acceptsItemDrop(stepIndex: number): boolean {
    const source = drag.source.value
    if (!source || source.kind === 'step') return false
    return props.canReceiveItem(stepIndex, { step: source.step, index: source.index })
}

/** Card-level dragover: step drags reorder the card, item drags append to it. */
function onCardDragOver(index: number, event: DragEvent): void {
    if (drag.source.value?.kind === 'step') {
        const insert = isPastMidpointY(event) ? index + 1 : index
        drag.over({ kind: 'step', insert }, event)
    } else if (acceptsItemDrop(index)) {
        drag.over({ kind: 'item', step: index, insert: props.steps[index]?.items.length ?? 0 }, event)
    }
}

/** Medallion-level dragover: precise insertion side from the pointer X. */
function onItemDragOver(stepIndex: number, itemIndex: number, event: DragEvent): void {
    if (drag.source.value?.kind === 'step' || !acceptsItemDrop(stepIndex)) return
    event.stopPropagation()
    const insert = isPastMidpointX(event) ? itemIndex + 1 : itemIndex
    drag.over({ kind: 'item', step: stepIndex, insert }, event)
}

function isPastMidpointY(event: DragEvent): boolean {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
    return event.clientY > rect.top + rect.height / 2
}

function isPastMidpointX(event: DragEvent): boolean {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
    return event.clientX > rect.left + rect.width / 2
}

function isDraggedStep(index: number): boolean {
    const source = drag.source.value
    return source?.kind === 'step' && source.index === index
}

function isDraggedItem(stepIndex: number, itemIndex: number): boolean {
    const source = drag.source.value
    return source?.kind === 'item' && source.step === stepIndex && source.index === itemIndex
}

function isStepInsertAt(at: number): boolean {
    const target = drag.target.value
    return target?.kind === 'step' && target.insert === at
}

function isItemInsertAt(stepIndex: number, at: number): boolean {
    const target = drag.target.value
    return target?.kind === 'item' && target.step === stepIndex && target.insert === at
}

function onLabelInput(index: number, event: Event): void {
    emit('editStep', index, { label: (event.target as HTMLInputElement).value })
}

function onNoteInput(index: number, event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value
    emit('editStep', index, { note: value.trim() === '' ? null : value })
}

function goldOf(itemId: string): number | null {
    return props.resolveItem(itemId)?.gold ?? null
}

function itemName(itemId: string): string {
    return props.resolveItem(itemId)?.name ?? itemId
}

function itemImage(itemId: string): string | null {
    return props.resolveItem(itemId)?.image ?? null
}

/** Tooltip: real name, suffixed with the honest reason when the item ghosts. */
function itemTitle(itemId: string): string {
    const reason = props.ghostOf(itemId)
    if (reason === null) return itemName(itemId)
    return `${itemName(itemId)} — ${reason === 'mode' ? props.ui.ghostMode : props.ui.ghost}`
}
</script>

<template>
    <div class="space-y-5">
        <datalist id="forge-step-presets">
            <option v-for="preset in labels.presets" :key="preset" :value="preset" />
        </datalist>

        <template v-for="(step, index) in steps" :key="index">
            <div v-if="isStepInsertAt(index)" class="forge-droprow" aria-hidden="true"></div>
            <article
                class="forge-step"
                :class="{ 'forge-dragging': isDraggedStep(index) }"
                @dragover="onCardDragOver(index, $event)"
                @drop.prevent="drag.drop($event)"
            >
                <div class="flex flex-wrap items-center gap-2">
                    <span
                        class="forge-drag-handle"
                        draggable="true"
                        aria-hidden="true"
                        :title="dnd.handle"
                        @dragstart="drag.start({ kind: 'step', index }, $event)"
                        @dragend="drag.end()"
                        >⣿</span
                    >
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

                <ul class="mt-3 flex flex-wrap items-start gap-3">
                    <template v-for="(itemId, itemIndex) in step.items" :key="`${itemIndex}-${itemId}`">
                        <li v-if="isItemInsertAt(index, itemIndex)" class="forge-dropslot" aria-hidden="true"></li>
                        <li class="text-center">
                            <span
                                class="forge-item forge-grab block"
                                :class="{
                                    'forge-ghost': ghostOf(itemId) !== null,
                                    'forge-dragging': isDraggedItem(index, itemIndex),
                                }"
                                :title="itemTitle(itemId)"
                                draggable="true"
                                @dragstart="drag.start({ kind: 'item', step: index, index: itemIndex }, $event)"
                                @dragend="drag.end()"
                                @dragover="onItemDragOver(index, itemIndex, $event)"
                            >
                                <img v-if="itemImage(itemId)" :src="itemImage(itemId) ?? undefined" :alt="itemName(itemId)"
                                     loading="lazy" decoding="async" />
                                <span v-else class="forge-item__fallback">{{ itemName(itemId) }}</span>
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
                    </template>
                    <li v-if="isItemInsertAt(index, step.items.length)" class="forge-dropslot" aria-hidden="true"></li>
                    <li>
                        <button
                            type="button"
                            class="forge-additem"
                            :aria-label="armory.addCta"
                            :title="armory.addCta"
                            @click="openArmory(index)"
                        >
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true">
                                <path d="M12 5v14M5 12h14" stroke-linecap="round" />
                            </svg>
                            <span class="forge-additem__label">{{ armory.addCta }}</span>
                        </button>
                    </li>
                </ul>

                <p class="forge-hint mt-3">
                    {{ formatCounter(ui.counter, step.items.length, MAX_ITEMS_PER_STEP) }}
                </p>
            </article>
        </template>
        <div v-if="isStepInsertAt(steps.length)" class="forge-droprow" aria-hidden="true"></div>

        <div class="flex items-center justify-between gap-3">
            <button type="button" class="hx-btn-ghost" :disabled="!canAddStepNow" @click="emit('addStep')">
                {{ labels.add }}
            </button>
            <span class="forge-hint">{{ formatCounter(ui.counter, steps.length, MAX_STEPS) }}</span>
        </div>

        <ItemArmory
            :open="armoryStepInfo !== null"
            :step="armoryStepInfo"
            :options="options"
            :is-loading="isLoading"
            :has-error="hasError"
            :can-add="armoryStepInfo !== null && canAddItemTo(armoryStepInfo.index)"
            :max-items="MAX_ITEMS_PER_STEP"
            :labels="armory"
            :ui="ui"
            @add="(itemId) => armoryStepInfo && emit('addItem', armoryStepInfo.index, itemId)"
            @close="armoryStep = null"
            @retry="emit('retry')"
        />
    </div>
</template>
