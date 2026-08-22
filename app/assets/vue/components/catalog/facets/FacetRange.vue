<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { FacetDefinition, RangeSelection } from '../../../filter/facets'
import type { RangeBounds } from '../../../filter/useCardGrid'
import { injectFacetStore } from '../../../filter/useFacetState'

/**
 * One numeric facet as a dual-thumb slider plus min/max fields, bounded by
 * what the grid carries. Dragging only moves the local thumbs; the selection
 * is committed on `change` (release / blur) so a drag never floods the URL.
 * The fields are read on `change` only: clamping each keystroke would rewrite
 * the field under the reader's fingers. A selection equal to the full bounds
 * is no selection at all.
 */
const props = defineProps<{
    facet: FacetDefinition
    selection?: RangeSelection
    bounds: RangeBounds
    labels: { min: string; max: string; clear: string }
}>()

const store = injectFacetStore()

const low = ref(props.selection?.min ?? props.bounds.min)
const high = ref(props.selection?.max ?? props.bounds.max)

watch(
    () => [props.selection, props.bounds] as const,
    ([selection, bounds]) => {
        low.value = selection?.min ?? bounds.min
        high.value = selection?.max ?? bounds.max
    },
)

const isActive = computed(() => props.selection !== undefined)
const decimals = computed(() => Math.max(0, Math.ceil(-Math.log10(props.facet.step))))
const span = computed(() => Math.max(props.bounds.max - props.bounds.min, props.facet.step))
const lowPercent = computed(() => ((low.value - props.bounds.min) / span.value) * 100)
const highPercent = computed(() => ((high.value - props.bounds.min) / span.value) * 100)
/** The lower thumb must stay reachable when both sit on the right edge. */
const lowOnTop = computed(() => highPercent.value >= 100 && lowPercent.value >= 99)

function clamp(value: number): number {
    if (!Number.isFinite(value)) return props.bounds.min
    return Math.min(props.bounds.max, Math.max(props.bounds.min, value))
}

function onLowInput(event: Event): void {
    low.value = Math.min(clamp(Number((event.target as HTMLInputElement).value)), high.value)
}

function onHighInput(event: Event): void {
    high.value = Math.max(clamp(Number((event.target as HTMLInputElement).value)), low.value)
}

function commitLowField(event: Event): void {
    onLowInput(event)
    commit()
}

function commitHighField(event: Event): void {
    onHighInput(event)
    commit()
}

function commit(): void {
    const min = Math.min(low.value, high.value)
    const max = Math.max(low.value, high.value)
    low.value = min
    high.value = max
    const isWhole = min <= props.bounds.min && max >= props.bounds.max
    store.setRange(props.facet.key, isWhole ? null : { min, max })
}

function format(value: number): string {
    return value.toFixed(decimals.value) + (props.facet.unit ? ` ${props.facet.unit}` : '')
}
</script>

<template>
    <div class="facet facet--range" role="group" :aria-label="facet.label">
        <div class="facet__head">
            <span class="facet__label">{{ facet.label }}</span>
            <span class="facet__readout" :class="{ 'facet__readout--on': isActive }"
                  aria-live="polite">{{ format(low) }} – {{ format(high) }}</span>
            <button v-if="isActive" type="button" class="facet__reset" :aria-label="labels.clear"
                    @click="store.clearFacet(facet.key)">×</button>
        </div>
        <div class="hx-range" :class="{ 'hx-range--low-on-top': lowOnTop }">
            <span class="hx-range__track" aria-hidden="true">
                <span class="hx-range__fill"
                      :style="{ left: `${lowPercent}%`, right: `${100 - highPercent}%` }"></span>
            </span>
            <input type="range" class="hx-range__input hx-range__input--low"
                   :min="bounds.min" :max="bounds.max" :step="facet.step" :value="low"
                   :aria-label="`${facet.label} · ${labels.min}`" :aria-valuetext="format(low)"
                   @input="onLowInput" @change="commit">
            <input type="range" class="hx-range__input hx-range__input--high"
                   :min="bounds.min" :max="bounds.max" :step="facet.step" :value="high"
                   :aria-label="`${facet.label} · ${labels.max}`" :aria-valuetext="format(high)"
                   @input="onHighInput" @change="commit">
        </div>
        <div class="facet__fields">
            <label class="facet__field">
                <span>{{ labels.min }}</span>
                <input type="number" :min="bounds.min" :max="bounds.max" :step="facet.step"
                       :value="low" @change="commitLowField">
            </label>
            <label class="facet__field">
                <span>{{ labels.max }}</span>
                <input type="number" :min="bounds.min" :max="bounds.max" :step="facet.step"
                       :value="high" @change="commitHighField">
            </label>
        </div>
    </div>
</template>
