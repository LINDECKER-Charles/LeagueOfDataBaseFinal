<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'

/**
 * Level slider for the server-rendered champion stat board. Vue owns only the
 * slider; the rows stay in Twig (visible without JS at level 1) and are driven
 * imperatively through `#{targetId} [data-stat]` — same contract style as
 * ResourceFilter. Growth follows the game's per-level curve
 * (g × (n−1) × (0.7025 + 0.0175 × (n−1))), which Data Dragon's flat
 * `perlevel` values feed; `percent` marks attack speed, whose growth is a
 * percentage of the base.
 */
const props = defineProps<{
    targetId: string
    labels: { level: string }
}>()

const MIN_LEVEL = 1
const MAX_LEVEL = 18

interface StatCell {
    valueEl: HTMLElement
    base: number
    growth: number
    kind: 'flat' | 'percent' | 'static'
}

const level = ref(MIN_LEVEL)
const cells = ref<StatCell[]>([])

const growthFactor = (n: number): number => (n - 1) * (0.7025 + 0.0175 * (n - 1))

function statAt(cell: StatCell, n: number): number {
    if (cell.kind === 'percent') {
        return cell.base * (1 + (cell.growth / 100) * growthFactor(n))
    }
    if (cell.kind === 'flat') {
        return cell.base + cell.growth * growthFactor(n)
    }
    return cell.base
}

function format(value: number): string {
    if (value < 10) return String(Math.round(value * 100) / 100)
    if (value < 1000) return String(Math.round(value * 10) / 10)
    return String(Math.round(value))
}

function apply(): void {
    for (const cell of cells.value) {
        cell.valueEl.textContent = format(statAt(cell, level.value))
    }
}

onMounted(() => {
    const root = document.getElementById(props.targetId)
    if (!root) return
    cells.value = Array.from(root.querySelectorAll<HTMLElement>('[data-stat]')).flatMap((el) => {
        const valueEl = el.querySelector<HTMLElement>('[data-stat-value]')
        if (!valueEl) return []
        return [{
            valueEl,
            base: Number(el.dataset.base ?? 0),
            growth: Number(el.dataset.growth ?? 0),
            kind: (el.dataset.kind ?? 'flat') as StatCell['kind'],
        }]
    })
})

watch(level, apply)
</script>

<template>
    <div class="lvl-slider mb-2">
        <div class="mb-1 flex items-baseline justify-between">
            <span class="font-mono text-[10px] uppercase tracking-[0.14em] text-text-dim">
                {{ labels.level.replace('%level%', String(level)) }}
            </span>
            <span class="font-beaufort text-sm text-gold">{{ level }} / {{ MAX_LEVEL }}</span>
        </div>
        <input
            v-model.number="level"
            type="range"
            :min="MIN_LEVEL"
            :max="MAX_LEVEL"
            step="1"
            :aria-label="labels.level.replace('%level%', String(level))"
        />
    </div>
</template>
