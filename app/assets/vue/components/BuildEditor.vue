<script setup lang="ts">
import { onMounted } from 'vue'
import ChampionPicker from '../builds/ChampionPicker.vue'
import RuneBoard from '../builds/RuneBoard.vue'
import StepEditor from '../builds/StepEditor.vue'
import { useBuildEditor, type BuildEditorProps } from '../builds/useBuildEditor'

/**
 * Build editor island. Mounted INSIDE the Twig <form>: it owns the champion /
 * runes / purchase-order sections and mirrors the whole selection into the
 * hidden `structure` input on every change (the server re-validates). All the
 * orchestration lives in useBuildEditor + the pure modules under builds/.
 */
const props = defineProps<BuildEditorProps>()

const {
    champions,
    items,
    runes,
    championId,
    runeDraft,
    steps,
    itemsById,
    structureJson,
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
    canAddStep,
    canAddItemTo,
} = useBuildEditor(props)

onMounted(() => void loadCatalogs())
</script>

<template>
    <div class="space-y-8">
        <input type="hidden" name="structure" :value="structureJson" />

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
                :items-by-id="itemsById"
                :options="items.data.value"
                :is-loading="items.isLoading.value"
                :has-error="items.hasError.value"
                :can-add-step-now="canAddStep"
                :can-add-item-to="canAddItemTo"
                :labels="labels.steps"
                :ui="labels"
                @add-step="appendStep()"
                @remove-step="deleteStep"
                @move-step="shiftStep"
                @edit-step="editStep"
                @add-item="appendItem"
                @remove-item="deleteItem"
                @move-item="shiftItem"
                @retry="items.retry()"
            />
        </section>
    </div>
</template>
