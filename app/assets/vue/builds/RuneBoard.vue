<script setup lang="ts">
import { computed } from 'vue'
import type { RunePerk, RuneTree } from './catalogTypes'
import { GHOST_SLOT, KEYSTONE_SLOT, type RuneDraft } from './runeRules'
import type { RunesLabels, UiLabels } from './editorLabels'

/**
 * Rune section: primary tree tabs -> 4 perk rows (keystone highlighted), then
 * the secondary tree (remaining 4) -> minor rows with the two-distinct-slots
 * rule enforced by the parent's pure rules. Perks selections carried by ids
 * that no longer exist on this patch render as ghost chips — visible, the user
 * decides whether to replace them.
 */
const props = defineProps<{
    trees: RuneTree[] | null
    isLoading: boolean
    hasError: boolean
    draft: RuneDraft
    labels: RunesLabels
    ui: UiLabels
}>()

const emit = defineEmits<{
    primaryStyle: [styleId: number]
    primaryPerk: [slotIndex: number, perkId: number]
    secondaryStyle: [styleId: number]
    secondaryPerk: [slotIndex: number, perkId: number]
    retry: []
}>()

const primaryTree = computed(() => (props.trees ?? []).find((t) => t.id === props.draft.primaryStyleId) ?? null)
const secondaryTree = computed(() => (props.trees ?? []).find((t) => t.id === props.draft.secondaryStyleId) ?? null)
const secondaryChoices = computed(() => (props.trees ?? []).filter((t) => t.id !== props.draft.primaryStyleId))

/** Tints the active board via the shared .path-* variables (detail.css). */
function pathClass(tree: RuneTree | null): string {
    return tree ? `path-${tree.key.toLowerCase()}` : ''
}

function slotName(slotIndex: number): string {
    return slotIndex === KEYSTONE_SLOT ? props.labels.keystone : props.labels.slot.replace('%n%', String(slotIndex))
}

function isPrimaryPick(slotIndex: number, perkId: number): boolean {
    return props.draft.primaryPerks[slotIndex] === perkId
}

function isSecondaryPick(perkId: number): boolean {
    return props.draft.secondaryPicks.some((pick) => pick.perkId === perkId)
}

function isSecondarySlotUsed(slotIndex: number): boolean {
    return props.draft.secondaryPicks.some((pick) => pick.slotIndex === slotIndex)
}

/** Stored primary pick of a slot that no longer exists in the catalog row. */
function primaryGhost(slotIndex: number, perks: RunePerk[]): number | null {
    const picked = props.draft.primaryPerks[slotIndex]
    if (picked === null || picked === undefined) return null
    return perks.some((perk) => perk.id === picked) ? null : picked
}

const secondaryGhosts = computed(() => props.draft.secondaryPicks.filter((p) => p.slotIndex === GHOST_SLOT))
</script>

<template>
    <div class="space-y-6">
        <p v-if="isLoading" class="forge-hint" role="status">{{ ui.loading }}</p>
        <div v-else-if="hasError" class="forge-error">
            {{ ui.error }}
            <button type="button" class="hx-btn-ghost forge-btn-sm ml-3" @click="emit('retry')">{{ ui.retry }}</button>
        </div>

        <template v-else-if="trees">
            <div :class="pathClass(primaryTree)">
                <p class="forge-hint mb-2.5">{{ labels.primary }}</p>
                <div class="forge-trees">
                    <button
                        v-for="tree in trees"
                        :key="tree.id"
                        type="button"
                        class="forge-tree"
                        :class="[pathClass(tree), { 'forge-tree--on': tree.id === draft.primaryStyleId }]"
                        :aria-pressed="tree.id === draft.primaryStyleId"
                        @click="emit('primaryStyle', tree.id)"
                    >
                        <img v-if="tree.icon" :src="tree.icon" alt="" loading="lazy" decoding="async" />
                        {{ tree.name }}
                    </button>
                </div>

                <div v-if="primaryTree" class="mt-4">
                    <div
                        v-for="(perks, slotIndex) in primaryTree.slots"
                        :key="slotIndex"
                        class="forge-slot"
                        :class="{ 'forge-slot--picked': draft.primaryPerks[slotIndex] !== null }"
                    >
                        <span class="forge-slot__name">{{ slotName(slotIndex) }}</span>
                        <button
                            v-for="perk in perks"
                            :key="perk.id"
                            type="button"
                            class="forge-perk"
                            :class="{ 'forge-perk--big': slotIndex === 0, 'forge-perk--on': isPrimaryPick(slotIndex, perk.id) }"
                            :aria-pressed="isPrimaryPick(slotIndex, perk.id)"
                            :title="perk.shortDesc ? `${perk.name} — ${perk.shortDesc}` : perk.name"
                            @click="emit('primaryPerk', slotIndex, perk.id)"
                        >
                            <img v-if="perk.icon" :src="perk.icon" :alt="perk.name" loading="lazy" decoding="async" />
                        </button>
                        <span v-if="primaryGhost(slotIndex, perks) !== null" class="hx-chip forge-ghost" :title="ui.ghost">
                            #{{ primaryGhost(slotIndex, perks) }}
                        </span>
                    </div>
                </div>
            </div>

            <div v-if="draft.primaryStyleId !== null" :class="pathClass(secondaryTree)">
                <p class="forge-hint mb-1">{{ labels.secondary }}</p>
                <p class="forge-hint mb-2.5 opacity-80">{{ labels.secondaryHint }}</p>
                <div class="forge-trees">
                    <button
                        v-for="tree in secondaryChoices"
                        :key="tree.id"
                        type="button"
                        class="forge-tree"
                        :class="[pathClass(tree), { 'forge-tree--on': tree.id === draft.secondaryStyleId }]"
                        :aria-pressed="tree.id === draft.secondaryStyleId"
                        @click="emit('secondaryStyle', tree.id)"
                    >
                        <img v-if="tree.icon" :src="tree.icon" alt="" loading="lazy" decoding="async" />
                        {{ tree.name }}
                    </button>
                </div>

                <div v-if="secondaryGhosts.length" class="mt-3 flex flex-wrap gap-2">
                    <span v-for="ghost in secondaryGhosts" :key="ghost.perkId" class="hx-chip forge-ghost" :title="ui.ghost">
                        #{{ ghost.perkId }}
                    </span>
                </div>

                <div v-if="secondaryTree" class="mt-4">
                    <template v-for="(perks, slotIndex) in secondaryTree.slots" :key="slotIndex">
                        <div
                            v-if="slotIndex > 0"
                            class="forge-slot"
                            :class="{ 'forge-slot--picked': isSecondarySlotUsed(slotIndex) }"
                        >
                            <span class="forge-slot__name">{{ slotName(slotIndex) }}</span>
                            <button
                                v-for="perk in perks"
                                :key="perk.id"
                                type="button"
                                class="forge-perk"
                                :class="{ 'forge-perk--on': isSecondaryPick(perk.id) }"
                                :aria-pressed="isSecondaryPick(perk.id)"
                                :title="perk.shortDesc ? `${perk.name} — ${perk.shortDesc}` : perk.name"
                                @click="emit('secondaryPerk', slotIndex, perk.id)"
                            >
                                <img v-if="perk.icon" :src="perk.icon" :alt="perk.name" loading="lazy" decoding="async" />
                            </button>
                        </div>
                    </template>
                </div>
            </div>
        </template>
    </div>
</template>
