<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { filterOptions } from '../picker/filterOptions'
import { useSkinCatalog, type SkinPickerEntry } from '../picker/useSkinCatalog'

/**
 * The profile banner favorite: a single wide socket that owns one hidden input
 * ("{championId}_{skinNum}"). Picking is a two-step flow in the shared picker
 * <dialog> — champion first, then one of its skins. Catalogue state/fetching
 * live in the composable; this SFC is presentation + wiring only.
 */
interface CurrentBanner {
    id: string
    name: string
    banner: string
}

interface Labels {
    pick: string
    change: string
    remove: string
    empty: string
    chooseChampion: string
    chooseSkin: string
    back: string
    close: string
    search: string
    loading: string
    error: string
    retry: string
    noResults: string
}

const props = defineProps<{
    fieldName: string
    current: CurrentBanner | null
    championsEndpoint: string
    skinsEndpoint: string
    version: string
    lang: string
    labels: Labels
}>()

const catalog = useSkinCatalog({
    championsEndpoint: props.championsEndpoint,
    skinsEndpoint: props.skinsEndpoint,
    version: props.version,
    lang: props.lang,
})

const value = ref(props.current?.id ?? '')
const display = ref<CurrentBanner | null>(props.current)

const step = ref<'champion' | 'skin'>('champion')
const champion = ref<{ id: string; name: string } | null>(null)
const query = ref('')
const root = ref<HTMLElement | null>(null)
const sheet = ref<HTMLDialogElement | null>(null)
const searchField = ref<HTMLInputElement | null>(null)

const skinState = computed(() => (champion.value ? catalog.skinsFor(champion.value.id) : null))
const activeState = computed(() => (step.value === 'champion' ? catalog.champions : skinState.value))
const visibleEntries = computed<SkinPickerEntry[]>(() =>
    activeState.value?.status === 'ready' ? filterOptions(activeState.value.entries, query.value) : [],
)
const headTitle = computed(() =>
    step.value === 'champion' ? props.labels.chooseChampion : (champion.value?.name ?? props.labels.chooseSkin),
)

async function open(): Promise<void> {
    step.value = 'champion'
    champion.value = null
    query.value = ''
    sheet.value?.showModal()
    void catalog.ensureChampions()
    await focusSearch()
}

function close(): void {
    sheet.value?.close()
}

function onBackdropClick(event: MouseEvent): void {
    if (event.target === sheet.value) {
        close()
    }
}

async function selectChampion(entry: SkinPickerEntry): Promise<void> {
    champion.value = { id: entry.id, name: entry.name }
    step.value = 'skin'
    query.value = ''
    void catalog.ensureSkins(entry.id)
    await focusSearch()
}

async function back(): Promise<void> {
    step.value = 'champion'
    query.value = ''
    await focusSearch()
}

function selectSkin(entry: SkinPickerEntry): void {
    value.value = entry.id
    display.value = { id: entry.id, name: entry.name, banner: entry.banner ?? entry.image ?? '' }
    notifyChange()
    close()
}

function removeCurrent(): void {
    value.value = ''
    display.value = null
    notifyChange()
    close()
}

/* Bubble to the host <form> so its auto-save enhancement can persist the pick. */
function notifyChange(): void {
    root.value?.dispatchEvent(new CustomEvent('profile:changed', { bubbles: true }))
}

function retry(): void {
    if (step.value === 'champion') {
        void catalog.ensureChampions()
    } else if (champion.value) {
        void catalog.ensureSkins(champion.value.id)
    }
}

async function focusSearch(): Promise<void> {
    await nextTick()
    searchField.value?.focus()
}

function webp(image: string): string {
    return image.replace(/\.png$/, '.webp')
}

function initials(name: string): string {
    return name.slice(0, 2).toUpperCase()
}
</script>

<template>
    <div ref="root">
        <button
            type="button"
            class="skin-socket"
            :class="display ? 'skin-socket--filled hextech-frame hx-corners' : 'skin-socket--empty'"
            :aria-label="`${labels.pick} — ${display?.name ?? labels.empty}`"
            @click="open"
        >
            <img v-if="display" class="skin-socket__art" :src="display.banner" alt="" loading="lazy" decoding="async" />
            <span class="skin-socket__scrim" aria-hidden="true"></span>
            <span class="skin-socket__body">
                <span class="skin-socket__type">{{ labels.pick }}</span>
                <span class="skin-socket__name" :class="{ 'skin-socket__name--muted': !display }">
                    {{ display?.name ?? labels.empty }}
                </span>
            </span>
            <span class="skin-socket__cta">{{ display ? labels.change : labels.pick }}</span>
        </button>

        <input type="hidden" :name="fieldName" :value="value" />

        <dialog
            ref="sheet"
            class="filter-sheet picker-sheet"
            :aria-label="headTitle"
            @click="onBackdropClick"
            @close="step = 'champion'"
        >
            <div class="filter-sheet__handle" aria-hidden="true"></div>
            <header class="picker-head">
                <button v-if="step === 'skin'" type="button" class="picker-back" :aria-label="labels.back" @click="back">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                        <path d="M15 6l-6 6 6 6" stroke-linecap="round" stroke-linejoin="round" />
                    </svg>
                </button>
                <p class="eyebrow">{{ headTitle }}</p>
                <button type="button" class="picker-close" :aria-label="labels.close" @click="close">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                        <path d="M6 6l12 12M18 6L6 18" stroke-linecap="round" />
                    </svg>
                </button>
            </header>

            <input
                ref="searchField"
                v-model="query"
                type="search"
                class="hx-input picker-search"
                :placeholder="labels.search"
                :aria-label="labels.search"
            />

            <div class="picker-list">
                <p v-if="activeState?.status === 'loading' || activeState?.status === 'idle'" class="picker-note">
                    {{ labels.loading }}
                </p>
                <div v-else-if="activeState?.status === 'error'" class="picker-note">
                    <p>{{ labels.error }}</p>
                    <button type="button" class="picker-retry" @click="retry">{{ labels.retry }}</button>
                </div>
                <template v-else>
                    <p v-if="visibleEntries.length === 0" class="picker-note">{{ labels.noResults }}</p>

                    <!-- Step 1: champion rows -->
                    <template v-else-if="step === 'champion'">
                        <button
                            v-for="entry in visibleEntries"
                            :key="entry.id"
                            type="button"
                            class="picker-option"
                            @click="selectChampion(entry)"
                        >
                            <picture v-if="entry.image" class="picker-option__img">
                                <source v-if="entry.image.endsWith('.png')" :srcset="webp(entry.image)" type="image/webp" />
                                <img :src="entry.image" alt="" width="32" height="32" loading="lazy" decoding="async" />
                            </picture>
                            <span v-else class="picker-option__img picker-option__img--initials" aria-hidden="true">
                                {{ initials(entry.name) }}
                            </span>
                            <span class="picker-option__name">{{ entry.name }}</span>
                        </button>
                    </template>

                    <!-- Step 2: skin tiles -->
                    <div v-else class="skin-grid">
                        <button
                            v-for="entry in visibleEntries"
                            :key="entry.id"
                            type="button"
                            class="skin-tile"
                            :class="{ 'skin-tile--active': entry.id === value }"
                            @click="selectSkin(entry)"
                        >
                            <img
                                v-if="entry.banner"
                                class="skin-tile__art"
                                :src="entry.banner"
                                :alt="entry.name"
                                loading="lazy"
                                decoding="async"
                            />
                            <span class="skin-tile__name">{{ entry.name }}</span>
                        </button>
                    </div>
                </template>
            </div>

            <footer v-if="value !== ''" class="picker-foot">
                <button type="button" class="picker-remove" @click="removeCurrent">{{ labels.remove }}</button>
            </footer>
        </dialog>
    </div>
</template>
