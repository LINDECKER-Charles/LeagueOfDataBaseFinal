<script setup lang="ts">
import { computed, ref } from 'vue'
import { useAbilityMedia, type AbilitySlot } from '../kit/useAbilityMedia'

/**
 * Champion ability showcase — the P/Q/W/E/R rail drives a panel that plays the
 * official looping preview of the selected spell (hotlinked from Riot's own
 * champion-page CDN), with per-rank cooldown/cost/range and the localized
 * description. A load failure on a slot's video degrades that slot to its
 * spell icon. Server renders a static ability list inside the mount as the
 * no-JS fallback; mounting replaces it.
 */
interface Ability {
    key: AbilitySlot
    name: string
    description: string
    icon: string | null
    cooldown: string | null
    cost: string | null
    range: string | null
    maxrank: number | null
    charges: number | null
}

interface Labels {
    passive: string
    cooldown: string
    cost: string
    range: string
    charges: string
    ranks: string
}

const props = defineProps<{
    championKey: string
    abilities: Ability[]
    resource: string
    labels: Labels
}>()

const media = useAbilityMedia(props.championKey)
const selected = ref(0)
const current = computed(() => props.abilities[selected.value] ?? props.abilities[0])

const showVideo = computed(() => media.isAvailable(current.value.key) && media.shouldAutoplay.value)
const showPoster = computed(() => media.isAvailable(current.value.key) && !media.shouldAutoplay.value)

const metaChips = computed(() => {
    const a = current.value
    const noCost = a.cost === null || a.cost === '0'
    return [
        { label: props.labels.cooldown, value: a.cooldown, unit: 's' },
        { label: props.labels.cost, value: noCost ? null : a.cost, unit: props.resource },
        { label: props.labels.range, value: a.range, unit: '' },
        { label: props.labels.charges, value: a.charges === null ? null : String(a.charges), unit: '' },
        { label: props.labels.ranks, value: a.key === 'P' || a.maxrank === null ? null : String(a.maxrank), unit: '' },
    ].filter((chip) => chip.value !== null && chip.value !== '')
})

function onRailKeydown(event: KeyboardEvent): void {
    const delta = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0
    if (delta === 0) return
    event.preventDefault()
    const n = props.abilities.length
    selected.value = (selected.value + delta + n) % n
    ;(event.currentTarget as HTMLElement)
        .querySelectorAll<HTMLButtonElement>('button')[selected.value]?.focus()
}
</script>

<template>
    <div>
        <div class="kit__rail" role="tablist" @keydown="onRailKeydown">
            <button
                v-for="(ability, i) in abilities"
                :key="ability.key"
                type="button"
                role="tab"
                class="kit__tab"
                :class="{ 'is-active': i === selected }"
                :aria-selected="i === selected"
                :aria-label="`${ability.key} — ${ability.name}`"
                :tabindex="i === selected ? 0 : -1"
                @click="selected = i"
            >
                <img v-if="ability.icon" :src="ability.icon" alt="" loading="lazy" decoding="async" />
                <span class="ability-key" :data-key="ability.key">{{ ability.key }}</span>
            </button>
        </div>

        <article class="kit__panel mt-5">
            <div v-if="showVideo" class="kit__media">
                <video
                    :key="current.key"
                    :poster="media.poster(current.key)"
                    autoplay
                    muted
                    loop
                    playsinline
                    preload="metadata"
                    aria-hidden="true"
                >
                    <source :src="media.webm(current.key)" type="video/webm" />
                    <source
                        :src="media.mp4(current.key)"
                        type="video/mp4"
                        @error="media.markUnavailable(current.key)"
                    />
                </video>
            </div>
            <div v-else-if="showPoster" class="kit__media">
                <img
                    class="kit__poster"
                    :src="media.poster(current.key)"
                    alt=""
                    decoding="async"
                    @error="media.markUnavailable(current.key)"
                />
            </div>
            <div v-else class="kit__media kit__media--idle">
                <img v-if="current.icon" :src="current.icon" :alt="current.name" />
            </div>

            <div class="p-5 sm:p-6">
                <div class="mb-3 flex flex-wrap items-center gap-2.5">
                    <span class="ability-key" :data-key="current.key">{{ current.key }}</span>
                    <h3 class="font-beaufort text-lg uppercase tracking-wide text-gold-bright">{{ current.name }}</h3>
                    <span v-if="current.key === 'P'" class="font-mono text-[10px] uppercase tracking-wider text-hex">
                        {{ labels.passive }}
                    </span>
                </div>

                <div v-if="metaChips.length" class="mb-4 flex flex-wrap gap-x-5 gap-y-1.5">
                    <span v-for="chip in metaChips" :key="chip.label" class="kit__stat">
                        {{ chip.label }} <b>{{ chip.value }}</b>
                        <template v-if="chip.unit">{{ chip.unit }}</template>
                    </span>
                </div>

                <!-- DDragon's own localized markup, styled by .ddragon-rich -->
                <!-- eslint-disable-next-line vue/no-v-html -->
                <div class="ddragon-rich text-sm text-text-muted" v-html="current.description"></div>
            </div>
        </article>
    </div>
</template>
