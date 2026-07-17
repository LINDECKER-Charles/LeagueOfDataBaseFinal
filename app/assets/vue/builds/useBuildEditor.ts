import { computed, ref, watch } from 'vue'
import {
    championOptions,
    itemOptions,
    runeTrees,
    secondarySlotIndex,
    type ItemOption,
    type RuneTree,
} from './catalogTypes'
import {
    draftFromRunes,
    draftRunes,
    emptyRuneDraft,
    GHOST_SLOT,
    isRuneDraftComplete,
    selectPrimaryPerk,
    selectPrimaryStyle,
    selectSecondaryPerk,
    selectSecondaryStyle,
    type RuneDraft,
} from './runeRules'
import {
    addItem,
    addStep,
    canAddItem,
    canAddStep,
    createStep,
    moveItem,
    moveStep,
    removeItem,
    removeStep,
    totalItems,
    updateStep,
} from './stepList'
import { parseStructure, serializeStructure, type BuildStep } from './structure'
import { usePickerCatalog } from './usePickerCatalog'

/** Shared catalog-state wording (loading / error / retry) + ghost + counters. */
export interface UiLabels {
    loading: string
    error: string
    retry: string
    ghost: string
    counter: string
}

export interface ChampionLabels {
    title: string
    search: string
    empty: string
    selected: string
}

export interface RunesLabels {
    title: string
    primary: string
    secondary: string
    keystone: string
    slot: string
    secondaryHint: string
}

export interface StepsLabels {
    title: string
    add: string
    remove: string
    moveUp: string
    moveDown: string
    label: string
    note: string
    searchItem: string
    itemEmpty: string
    removeItem: string
    gold: string
    presets: string[]
}

/** Labels contract of the build-editor island (translated server-side). */
export interface BuildEditorLabels extends UiLabels {
    champion: ChampionLabels
    runes: RunesLabels
    steps: StepsLabels
}

export interface BuildEditorProps {
    mode: 'create' | 'edit'
    initial: unknown
    endpoints: { champions: string; items: string; runes: string }
    version: string
    lang: string
    labels: BuildEditorLabels
}

/** "%count% / %max%" template substitution for the limit counters. */
export function formatCounter(template: string, count: number, max: number): string {
    return template.replace('%count%', String(count)).replace('%max%', String(max))
}

/**
 * Orchestrates the editor island: parses the initial structure, wires the pure
 * rune/step rules to reactive state, lazily loads the three picker catalogs and
 * keeps the serialized `structure` JSON in sync for the hidden form input.
 */
export function useBuildEditor(props: BuildEditorProps) {
    const withContext = (endpoint: string): string =>
        `${endpoint}?version=${encodeURIComponent(props.version)}&lang=${encodeURIComponent(props.lang)}`

    const champions = usePickerCatalog(() => withContext(props.endpoints.champions), championOptions)
    const items = usePickerCatalog(() => withContext(props.endpoints.items), itemOptions)
    const runes = usePickerCatalog(() => withContext(props.endpoints.runes), runeTrees)

    const initial = parseStructure(props.initial)
    const championId = ref(initial?.championId ?? '')
    const steps = ref<BuildStep[]>(initial && initial.steps.length > 0 ? initial.steps : [createStep()])
    // Secondary slot indexes need the runes catalog; until it lands the initial
    // picks carry GHOST_SLOT and are re-anchored by the watcher below.
    const runeDraft = ref<RuneDraft>(initial ? draftFromRunes(initial.runes, () => null) : emptyRuneDraft())

    watch(runes.data, (trees) => {
        if (trees) reanchorSecondaryPicks(trees)
    })

    /** Resolve GHOST_SLOT picks against the freshly loaded catalog (true ghosts stay). */
    function reanchorSecondaryPicks(trees: RuneTree[]): void {
        const draft = runeDraft.value
        const styleId = draft.secondaryStyleId
        if (styleId === null || draft.secondaryPicks.every((p) => p.slotIndex !== GHOST_SLOT)) {
            return
        }
        runeDraft.value = {
            ...draft,
            secondaryPicks: draft.secondaryPicks.map((pick) => ({
                ...pick,
                slotIndex:
                    pick.slotIndex === GHOST_SLOT
                        ? (secondarySlotIndex(trees, styleId, pick.perkId) ?? GHOST_SLOT)
                        : pick.slotIndex,
            })),
        }
    }

    const itemsById = computed<Map<string, ItemOption>>(() => {
        const index = new Map<string, ItemOption>()
        for (const option of items.data.value ?? []) index.set(option.id, option)
        return index
    })
    const goldOf = (itemId: string): number | null => itemsById.value.get(itemId)?.gold ?? null

    const structureJson = computed(() =>
        serializeStructure({
            championId: championId.value,
            runes: draftRunes(runeDraft.value),
            steps: steps.value,
        }),
    )

    const isRunesComplete = computed(() => isRuneDraftComplete(runeDraft.value))
    const itemsUsed = computed(() => totalItems(steps.value))

    return {
        champions,
        items,
        runes,
        championId,
        runeDraft,
        steps,
        itemsById,
        goldOf,
        structureJson,
        isRunesComplete,
        itemsUsed,
        loadCatalogs: () => Promise.all([champions.load(), items.load(), runes.load()]),
        setChampion: (id: string) => void (championId.value = id),
        setPrimaryStyle: (styleId: number) => void (runeDraft.value = selectPrimaryStyle(runeDraft.value, styleId)),
        setPrimaryPerk: (slot: number, perkId: number) =>
            void (runeDraft.value = selectPrimaryPerk(runeDraft.value, slot, perkId)),
        setSecondaryStyle: (styleId: number) =>
            void (runeDraft.value = selectSecondaryStyle(runeDraft.value, styleId)),
        setSecondaryPerk: (slot: number, perkId: number) =>
            void (runeDraft.value = selectSecondaryPerk(runeDraft.value, slot, perkId)),
        appendStep: (label = '') => void (steps.value = addStep(steps.value, label)),
        deleteStep: (index: number) => void (steps.value = removeStep(steps.value, index)),
        shiftStep: (index: number, delta: number) => void (steps.value = moveStep(steps.value, index, delta)),
        editStep: (index: number, patch: Partial<Pick<BuildStep, 'label' | 'note'>>) =>
            void (steps.value = updateStep(steps.value, index, patch)),
        appendItem: (stepIndex: number, itemId: string) =>
            void (steps.value = addItem(steps.value, stepIndex, itemId)),
        deleteItem: (stepIndex: number, itemIndex: number) =>
            void (steps.value = removeItem(steps.value, stepIndex, itemIndex)),
        shiftItem: (stepIndex: number, itemIndex: number, delta: number) =>
            void (steps.value = moveItem(steps.value, stepIndex, itemIndex, delta)),
        canAddStep: computed(() => canAddStep(steps.value)),
        canAddItemTo: (stepIndex: number) => canAddItem(steps.value, stepIndex),
    }
}
