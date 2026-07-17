<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { filterOptions, type PickerEntry } from '../picker/filterOptions'
import { usePickerCatalog, type SlotType } from '../picker/usePickerCatalog'

/**
 * The four favorite sockets of /profile. The island owns the hidden inputs of
 * the surrounding Twig form; picking happens in a shared <dialog> (bottom sheet
 * on mobile, centered from md up — same .filter-sheet grammar as the list
 * filters). Catalogue state/filtering live in the picker composable + pure
 * module; this SFC is presentation and wiring only.
 */
interface CurrentEntity {
    id: string
    name: string
    image: string | null
}

interface SlotProp {
    type: SlotType
    fieldName: string
    typeLabel: string
    current: CurrentEntity | null
    storedId: string | null
    emptyLabel: string
}

interface Labels {
    search: string
    remove: string
    close: string
    loading: string
    error: string
    retry: string
    noResults: string
    unavailable: string
}

const props = defineProps<{
    slots: SlotProp[]
    endpoints: Record<SlotType, string>
    version: string
    lang: string
    labels: Labels
}>()

const catalog = usePickerCatalog({ endpoints: props.endpoints, version: props.version, lang: props.lang })

/* Hidden-input value per slot. An unresolvable stored id round-trips untouched
   so the server warns on save instead of the island silently clearing it. */
const values = ref<string[]>(props.slots.map((slot) => slot.storedId ?? slot.current?.id ?? ''))
const displays = ref<(CurrentEntity | null)[]>(props.slots.map((slot) => slot.current))

const activeIndex = ref<number | null>(null)
const query = ref('')
const sheet = ref<HTMLDialogElement | null>(null)
const searchField = ref<HTMLInputElement | null>(null)

const sockets = computed(() =>
    props.slots.map((slot, index) => ({
        slot,
        index,
        display: displays.value[index] ?? null,
        value: values.value[index] ?? '',
    })),
)
const activeSlot = computed(() => (activeIndex.value === null ? null : (props.slots[activeIndex.value] ?? null)))
const activeState = computed(() => (activeSlot.value ? catalog.states[activeSlot.value.type] : null))
const activeValue = computed(() => (activeIndex.value === null ? '' : (values.value[activeIndex.value] ?? '')))
const visibleEntries = computed<PickerEntry[]>(() =>
    activeState.value?.status === 'ready' ? filterOptions(activeState.value.entries, query.value) : [],
)

function socketStateClass(display: CurrentEntity | null, value: string): string {
    if (display) {
        return 'hextech-frame hx-corners socket--filled'
    }
    return value !== '' ? 'socket--unavailable' : 'socket--empty'
}

function socketCaption(display: CurrentEntity | null, value: string, emptyLabel: string): string {
    if (display) {
        return display.name
    }
    return value !== '' ? props.labels.unavailable : emptyLabel
}

async function open(index: number): Promise<void> {
    activeIndex.value = index
    query.value = ''
    sheet.value?.showModal()
    const slot = props.slots[index]
    if (slot) {
        void catalog.ensure(slot.type)
    }
    await nextTick()
    searchField.value?.focus()
}

function close(): void {
    sheet.value?.close()
}

function onBackdropClick(event: MouseEvent): void {
    if (event.target === sheet.value) {
        close()
    }
}

function select(entry: PickerEntry): void {
    const index = activeIndex.value
    if (index === null) {
        return
    }
    values.value[index] = entry.id
    displays.value[index] = { id: entry.id, name: entry.name, image: entry.image }
    close()
}

function removeCurrent(): void {
    const index = activeIndex.value
    if (index === null) {
        return
    }
    values.value[index] = ''
    displays.value[index] = null
    close()
}

function retry(): void {
    const slot = activeSlot.value
    if (slot) {
        void catalog.ensure(slot.type)
    }
}

function webp(image: string): string {
    return image.replace(/\.png$/, '.webp')
}

function initials(name: string): string {
    return name.slice(0, 2).toUpperCase()
}
</script>

<template>
    <div>
        <div class="socket-grid">
            <button
                v-for="s in sockets"
                :key="s.slot.fieldName"
                type="button"
                class="socket socket--action"
                :class="socketStateClass(s.display, s.value)"
                :aria-label="`${s.slot.typeLabel} — ${socketCaption(s.display, s.value, s.slot.emptyLabel)}`"
                @click="open(s.index)"
            >
                <span class="socket__type">{{ s.slot.typeLabel }}</span>
                <template v-if="s.display">
                    <picture v-if="s.display.image" class="socket__portrait">
                        <source v-if="s.display.image.endsWith('.png')" :srcset="webp(s.display.image)" type="image/webp" />
                        <img :src="s.display.image" :alt="s.display.name" width="72" height="72" loading="lazy" decoding="async" />
                    </picture>
                    <span v-else class="socket__portrait socket__portrait--initials" aria-hidden="true">
                        {{ initials(s.display.name) }}
                    </span>
                    <span class="socket__name">{{ s.display.name }}</span>
                </template>
                <template v-else>
                    <span class="socket__hex" aria-hidden="true"></span>
                    <span class="socket__name socket__name--muted">{{ socketCaption(s.display, s.value, s.slot.emptyLabel) }}</span>
                </template>
            </button>
        </div>

        <input v-for="s in sockets" :key="s.slot.fieldName" type="hidden" :name="s.slot.fieldName" :value="s.value" />

        <dialog
            ref="sheet"
            class="filter-sheet picker-sheet"
            :aria-label="activeSlot?.typeLabel ?? ''"
            @click="onBackdropClick"
            @close="activeIndex = null"
        >
            <div class="filter-sheet__handle" aria-hidden="true"></div>
            <header class="picker-head">
                <p class="eyebrow">{{ activeSlot?.typeLabel }}</p>
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
                    <button
                        v-for="pickerEntry in visibleEntries"
                        :key="pickerEntry.id"
                        type="button"
                        class="picker-option"
                        :class="{
                            'picker-option--group': pickerEntry.isGroup,
                            'picker-option--indent': pickerEntry.groupId !== undefined,
                            'picker-option--active': pickerEntry.id === activeValue,
                        }"
                        @click="select(pickerEntry)"
                    >
                        <picture v-if="pickerEntry.image" class="picker-option__img">
                            <source v-if="pickerEntry.image.endsWith('.png')" :srcset="webp(pickerEntry.image)" type="image/webp" />
                            <img :src="pickerEntry.image" alt="" width="32" height="32" loading="lazy" decoding="async" />
                        </picture>
                        <span v-else class="picker-option__img picker-option__img--initials" aria-hidden="true">
                            {{ initials(pickerEntry.name) }}
                        </span>
                        <span class="picker-option__name">{{ pickerEntry.name }}</span>
                    </button>
                </template>
            </div>

            <footer v-if="activeValue !== ''" class="picker-foot">
                <button type="button" class="picker-remove" @click="removeCurrent">{{ labels.remove }}</button>
            </footer>
        </dialog>
    </div>
</template>
