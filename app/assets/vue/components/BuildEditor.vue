<script setup lang="ts">
import { onMounted } from 'vue'
import ChampionPicker from '../builds/ChampionPicker.vue'
import RuneBoard from '../builds/RuneBoard.vue'
import StepEditor from '../builds/StepEditor.vue'
import { useBuildEditor, type BuildEditorProps } from '../builds/useBuildEditor'

/**
 * Build editor island. Mounted INSIDE the Twig <form>: it owns the game
 * context (version + mode selects, real form fields), champion / runes /
 * purchase-order sections, and mirrors the whole selection into the hidden
 * `structure` input on every change (the server re-validates). All the
 * orchestration lives in useBuildEditor + the pure modules under builds/.
 */
const props = defineProps<BuildEditorProps>()

const {
    champions,
    items,
    runes,
    gameVersion,
    gameMode,
    championId,
    runeDraft,
    steps,
    resolveItem,
    ghostOf,
    structureJson,
    announcement,
    loadCatalogs,
    setChampion,
    setPrimaryStyle,
    setPrimaryPerk,
    setSecondaryStyle,
    setSecondaryPerk,
    appendStep,
    deleteStep,
    shiftStep,
    editStep,
    appendItem,
    deleteItem,
    shiftItem,
    dropStep,
    dropItem,
    announceDragCancelled,
    canAddStep,
    canAddItemTo,
    canReceiveItem,
} = useBuildEditor(props)

onMounted(() => void loadCatalogs())
</script>

<template>
    <div class="space-y-8">
        <input type="hidden" name="structure" :value="structureJson" />
        <!-- Polite announcements for drag-and-drop and button reorders. -->
        <div class="sr-only" role="status" aria-live="polite">{{ announcement }}</div>

        <section id="forge-context" class="hextech-frame hx-corners forge-section">
            <div class="codex-header mb-6"><h2>{{ labels.context.title }}</h2></div>
            <div class="forge-context">
                <label class="block">
                    <span class="auth-label">{{ labels.context.version }}</span>
                    <select v-model="gameVersion" name="game_version" class="hx-select mt-1.5 w-full">
                        <option v-for="patch in versions" :key="patch" :value="patch">{{ patch }}</option>
                    </select>
                </label>
                <label class="block">
                    <span class="auth-label">{{ labels.context.mode }}</span>
                    <select v-model="gameMode" name="game_mode" class="hx-select mt-1.5 w-full">
                        <option v-for="option in gameModes" :key="option.value" :value="option.value">
                            {{ option.label }}
                        </option>
                    </select>
                </label>
            </div>
            <p class="forge-hint mt-3">{{ labels.context.modeHint }}</p>
        </section>

        <section id="forge-champion" class="hextech-frame hx-corners forge-section">
            <div class="codex-header mb-6"><h2>{{ labels.champion.title }}</h2></div>
            <ChampionPicker
                :options="champions.data.value"
                :is-loading="champions.isLoading.value"
                :has-error="champions.hasError.value"
                :selected-id="championId"
                :labels="labels.champion"
                :ui="labels"
                @select="setChampion"
                @retry="champions.retry()"
            />
        </section>

        <section id="forge-runes" class="hextech-frame hx-corners forge-section">
            <div class="codex-header mb-6"><h2>{{ labels.runes.title }}</h2></div>
            <RuneBoard
                :trees="runes.data.value"
                :is-loading="runes.isLoading.value"
                :has-error="runes.hasError.value"
                :draft="runeDraft"
                :labels="labels.runes"
                :ui="labels"
                @primary-style="setPrimaryStyle"
                @primary-perk="setPrimaryPerk"
                @secondary-style="setSecondaryStyle"
                @secondary-perk="setSecondaryPerk"
                @retry="runes.retry()"
            />
        </section>

        <section id="forge-steps" class="hextech-frame hx-corners forge-section">
            <div class="codex-header mb-6"><h2>{{ labels.steps.title }}</h2></div>
            <StepEditor
                :steps="steps"
                :resolve-item="resolveItem"
                :ghost-of="ghostOf"
                :options="items.data.value"
                :is-loading="items.isLoading.value"
                :has-error="items.hasError.value"
                :can-add-step-now="canAddStep"
                :can-add-item-to="canAddItemTo"
                :can-receive-item="canReceiveItem"
                :labels="labels.steps"
                :armory="labels.armory"
                :dnd="labels.dnd"
                :ui="labels"
                @add-step="appendStep()"
                @remove-step="deleteStep"
                @move-step="shiftStep"
                @edit-step="editStep"
                @add-item="appendItem"
                @remove-item="deleteItem"
                @move-item="shiftItem"
                @reorder-step="dropStep"
                @move-item-to="dropItem"
                @drag-cancelled="announceDragCancelled"
                @retry="items.retry()"
            />
        </section>
    </div>
</template>
