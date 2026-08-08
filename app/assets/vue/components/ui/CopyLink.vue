<script setup lang="ts">
import { ref } from 'vue'

/**
 * One-click share-link copy. Clipboard API when available; otherwise (or on
 * failure) falls back to a readonly, auto-selected input the user copies from.
 */
const COPIED_RESET_MS = 2000

const props = defineProps<{
    url: string
    labels: { copy: string; copied: string; error: string }
}>()

const isCopied = ref(false)
const showFallback = ref(false)
const fallbackInput = ref<HTMLInputElement | null>(null)
let resetTimer: ReturnType<typeof setTimeout> | undefined

async function copy(): Promise<void> {
    if (!navigator.clipboard?.writeText) {
        openFallback()
        return
    }
    try {
        await navigator.clipboard.writeText(props.url)
        isCopied.value = true
        clearTimeout(resetTimer)
        resetTimer = setTimeout(() => (isCopied.value = false), COPIED_RESET_MS)
    } catch {
        openFallback()
    }
}

function openFallback(): void {
    showFallback.value = true
    // Wait for the input to render, then hand it over selected.
    requestAnimationFrame(() => {
        fallbackInput.value?.focus()
        fallbackInput.value?.select()
    })
}
</script>

<template>
    <div class="flex flex-col items-start gap-2">
        <button type="button" class="hx-btn-ghost" :aria-live="'polite'" @click="copy">
            <span v-if="isCopied">{{ labels.copied }}</span>
            <span v-else>{{ labels.copy }}</span>
        </button>
        <label v-if="showFallback" class="w-full">
            <span class="sr-only">{{ labels.error }}</span>
            <input
                ref="fallbackInput"
                class="hx-input font-mono text-xs"
                type="text"
                readonly
                :value="url"
                :title="labels.error"
                @focus="fallbackInput?.select()"
            />
        </label>
    </div>
</template>
