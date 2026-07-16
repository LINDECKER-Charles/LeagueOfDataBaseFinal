<script setup lang="ts">
import { computed, ref } from 'vue'

/**
 * Numeric navigation island — replaces the inline `oninput=...replace('REPLACE_ME')`
 * hacks used for page-jump and results-per-page. Twig passes a Symfony-generated
 * URL carrying the "__P__" placeholder; the component substitutes the typed value.
 */
const props = defineProps<{
    pathTemplate: string // e.g. "/champions?numpage=__P__&itemperpage=8"
    min?: number
    max?: number
    value?: number
    placeholder?: string
    label?: string // button label (e.g. "OK")
}>()

const current = ref<number | null>(props.value ?? null)

function clamp(n: number): number {
    const lo = props.min ?? 1
    const hi = props.max ?? Number.MAX_SAFE_INTEGER
    return Math.min(Math.max(n, lo), hi)
}

const href = computed(() => {
    const n = current.value == null || Number.isNaN(current.value) ? props.min ?? 1 : clamp(current.value)
    return props.pathTemplate.replace('__P__', String(n))
})

function go(): void {
    window.location.href = href.value
}
</script>

<template>
    <div class="inline-flex items-center gap-2">
        <input
            type="number"
            :min="min ?? 1"
            :max="max"
            :value="current ?? ''"
            :placeholder="placeholder"
            class="h-9 w-14 rounded-none border border-gold-deep/60 bg-void px-2 text-center font-mono text-sm text-text focus:border-hex focus:outline-none focus:ring-1 focus:ring-hex/50"
            @input="current = ($event.target as HTMLInputElement).valueAsNumber"
            @keydown.enter.prevent="go"
        />
        <a
            :href="href"
            class="inline-flex h-9 items-center border border-gold-deep bg-gold/10 px-3 font-beaufort text-sm uppercase tracking-widest text-gold transition-colors hover:bg-gold/20 hover:text-gold-bright"
        >
            {{ label ?? 'OK' }}
        </a>
    </div>
</template>
