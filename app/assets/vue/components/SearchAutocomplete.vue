<script setup lang="ts">
import { ref } from 'vue'
import AutoComplete, { type AutoCompleteCompleteEvent } from 'primevue/autocomplete'

/**
 * Live search island — replaces the three near-duplicate public/js/ajax*.js files.
 * Twig provides the resource-specific endpoints via data-props.
 */
const props = defineProps<{
    searchBase: string      // e.g. "/api/champions/search"
    detailBase: string      // e.g. "/champion"
    placeholder?: string
    version?: string
    lang?: string
}>()

interface Suggestion {
    id: string
    name: string
    image: string
}

const items = ref<Suggestion[]>([])
const selected = ref<Suggestion | string | null>(null)

async function search(event: AutoCompleteCompleteEvent): Promise<void> {
    const query = event.query.trim()
    if (query.length < 2) {
        items.value = []
        return
    }

    const params = new URLSearchParams()
    if (props.version) params.set('version', props.version)
    if (props.lang) params.set('lang', props.lang)
    const qs = params.toString()

    try {
        const res = await fetch(`${props.searchBase}/${encodeURIComponent(query)}${qs ? `?${qs}` : ''}`)
        items.value = res.ok ? await res.json() : []
    } catch {
        items.value = []
    }
}

function onSelect(): void {
    const choice = selected.value
    if (choice && typeof choice !== 'string') {
        window.location.href = `${props.detailBase}/${encodeURIComponent(choice.id)}`
    }
}
</script>

<template>
    <div class="hextech-search relative w-full">
        <span class="pointer-events-none absolute left-3 top-1/2 z-10 -translate-y-1/2 text-gold/70">
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="11" cy="11" r="7" /><path d="m21 21-4.3-4.3" />
            </svg>
        </span>
        <AutoComplete
            v-model="selected"
            :suggestions="items"
            option-label="name"
            :placeholder="placeholder ?? 'Rechercher…'"
            :complete-on-focus="false"
            :min-length="2"
            :pt="{ pcInputText: { root: { class: 'hextech-search-input' } } }"
            class="w-full"
            @complete="search"
            @option-select="onSelect"
        >
            <template #option="{ option }">
                <div class="flex items-center gap-3">
                    <img
                        v-if="option.image"
                        :src="`/${option.image}`"
                        alt=""
                        class="h-7 w-7 border border-gold-deep/50 object-cover"
                        loading="lazy"
                    />
                    <span class="text-sm text-text">{{ option.name }}</span>
                </div>
            </template>
        </AutoComplete>
    </div>
</template>
