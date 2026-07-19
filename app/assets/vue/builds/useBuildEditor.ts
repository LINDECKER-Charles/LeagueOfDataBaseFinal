import { computed, nextTick, ref, watch, type Ref } from 'vue'
import {
    championOptions,
    itemOptions,
    runeTrees,
    secondarySlotIndex,
    type ItemOption,
    type RuneTree,
} from './catalogTypes'
import { formatTemplate, type BuildEditorLabels } from './editorLabels'
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
    MAX_ITEMS_PER_STEP,
    moveItem,
    moveStep,
    moveItemToIndex,
    moveStepToIndex,
    removeItem,
    removeStep,
    totalItems,
    transferItem,
    updateStep,
    type ItemLocation,
} from './stepList'
import { parseStructure, serializeStructure, type BuildStep } from './structure'
import { usePickerCatalog } from './usePickerCatalog'
import { BUILD_WARM_PATH, requestWarm } from '../loader/warmBridge'

export interface GameModeOption {
    value: string
    label: string
}

export interface BuildEditorProps {
    mode: 'create' | 'edit'
    initial: unknown
    endpoints: { champions: string; items: string; runes: string }
    /** Initially selected patch (build's own on edit, site selection on create). */
    version: string
    /** Patches offered by the version select, latest first. */
    versions: string[]
    lang: string
    /** Initially selected game mode value (GameMode enum wire values). */
    gameMode: string
    gameModes: GameModeOption[]
    labels: BuildEditorLabels
}

/** Why a placed item renders ghosted; null when it is fine (or unknown yet). */
export type GhostReason = 'patch' | 'mode' | null

/**
 * Catalog trio bound to the LIVE (version, mode) context. Version switches
 * reload all three; mode switches only reload items (the sole mode-scoped
 * dataset). `knownItems` accumulates every item option seen this session so a
 * medallion keeps its identity (name/icon/gold) after the context that knew it
 * is switched away.
 */
function useEditorCatalogs(props: BuildEditorProps, gameVersion: Ref<string>, gameMode: Ref<string>) {
    const query = (endpoint: string, extra = ''): string =>
        `${endpoint}?version=${encodeURIComponent(gameVersion.value)}&lang=${encodeURIComponent(props.lang)}${extra}`

    const champions = usePickerCatalog(() => query(props.endpoints.champions), championOptions)
    const items = usePickerCatalog(
        () => query(props.endpoints.items, `&mode=${encodeURIComponent(gameMode.value)}`),
        itemOptions,
    )
    const runes = usePickerCatalog(() => query(props.endpoints.runes), runeTrees)

    // Switching to a patch whose images aren't warm yet loads a full catalog set:
    // gate it behind the global loader (real SSE progress over the ingestion) so
    // the reloaded pickers land on real icons, not placeholders. The initial patch
    // loaded un-gated on mount, and each warmed patch is only gated once a session.
    const warmedPatches = new Set<string>([props.version])

    async function switchVersion(version: string): Promise<void> {
        if (!warmedPatches.has(version)) {
            await requestWarm(version, props.lang, BUILD_WARM_PATH)
            warmedPatches.add(version)
        }
        await Promise.all([champions.reload(), items.reload(), runes.reload()])
    }

    watch(gameVersion, (version) => void switchVersion(version))
    watch(gameMode, () => void items.reload())

    const knownItems = ref<Record<string, ItemOption>>({})
    watch(items.data, (options) => {
        if (!options) return
        const merged = { ...knownItems.value }
        for (const option of options) merged[option.id] = option
        knownItems.value = merged
    })

    return { champions, items, runes, knownItems }
}

/** Polite screen-reader announcements; clearing first re-fires identical texts. */
function useAnnouncer() {
    const announcement = ref('')
    const announce = (message: string): void => {
        announcement.value = ''
        void nextTick(() => {
            announcement.value = message
        })
    }

    return { announcement, announce }
}

/**
 * Orchestrates the editor island: parses the initial structure, wires the pure
 * rune/step rules to reactive state, loads the picker catalogs for the LIVE
 * (version, mode) context and keeps the serialized `structure` JSON in sync
 * for the hidden form input. Every move (buttons and drag-and-drop) funnels
 * through the pure stepList helpers and announces politely.
 */
export function useBuildEditor(props: BuildEditorProps) {
    const gameVersion = ref(props.version)
    const gameMode = ref(props.gameMode)
    const { champions, items, runes, knownItems } = useEditorCatalogs(props, gameVersion, gameMode)
    const { announcement, announce } = useAnnouncer()
    const dnd = props.labels.dnd

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

    /** Display identity of a placed item: current catalog first, then any seen. */
    const resolveItem = (itemId: string): ItemOption | undefined =>
        itemsById.value.get(itemId) ?? knownItems.value[itemId]

    const goldOf = (itemId: string): number | null => resolveItem(itemId)?.gold ?? null

    /** Ghost verdict once the ACTIVE catalog answered; never judges while loading. */
    const ghostOf = (itemId: string): GhostReason => {
        if (items.data.value === null || itemsById.value.has(itemId)) return null
        return knownItems.value[itemId] ? 'mode' : 'patch'
    }

    /** Commits a step-list change and announces it; identity results stay silent. */
    function commitSteps(next: BuildStep[], message: string, params: Record<string, number>): void {
        if (next === steps.value) return
        steps.value = next
        announce(formatTemplate(message, params))
    }

    const structureJson = computed(() =>
        serializeStructure({
            championId: championId.value,
            runes: draftRunes(runeDraft.value),
            steps: steps.value,
        }),
    )

    return {
        champions,
        items,
        runes,
        gameVersion,
        gameMode,
        championId,
        runeDraft,
        steps,
        itemsById,
        resolveItem,
        goldOf,
        ghostOf,
        structureJson,
        announcement,
        announce,
        isRunesComplete: computed(() => isRuneDraftComplete(runeDraft.value)),
        itemsUsed: computed(() => totalItems(steps.value)),
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
        shiftStep: (index: number, delta: number) =>
            commitSteps(moveStep(steps.value, index, delta), dnd.movedStep, { position: index + delta + 1 }),
        editStep: (index: number, patch: Partial<Pick<BuildStep, 'label' | 'note'>>) =>
            void (steps.value = updateStep(steps.value, index, patch)),
        appendItem: (stepIndex: number, itemId: string) =>
            commitSteps(addItem(steps.value, stepIndex, itemId), dnd.added, { step: stepIndex + 1 }),
        deleteItem: (stepIndex: number, itemIndex: number) =>
            void (steps.value = removeItem(steps.value, stepIndex, itemIndex)),
        shiftItem: (stepIndex: number, itemIndex: number, delta: number) =>
            commitSteps(moveItem(steps.value, stepIndex, itemIndex, delta), dnd.movedItem, {
                position: itemIndex + delta + 1,
            }),
        dropStep: (from: number, insert: number) =>
            commitSteps(moveStepToIndex(steps.value, from, insert), dnd.movedStep, {
                position: (insert > from ? insert - 1 : insert) + 1,
            }),
        dropItem: (from: ItemLocation, to: ItemLocation) =>
            from.step === to.step
                ? commitSteps(moveItemToIndex(steps.value, from.step, from.index, to.index), dnd.movedItem, {
                      position: (to.index > from.index ? to.index - 1 : to.index) + 1,
                  })
                : commitSteps(transferItem(steps.value, from, to), dnd.transferred, { step: to.step + 1 }),
        announceDragCancelled: () => announce(dnd.cancelled),
        canAddStep: computed(() => canAddStep(steps.value)),
        canAddItemTo: (stepIndex: number) => canAddItem(steps.value, stepIndex),
        // A same-step move never changes counts; a cross-step one only fights
        // the target's per-step cap (the build total is untouched).
        canReceiveItem: (stepIndex: number, from: ItemLocation): boolean =>
            from.step === stepIndex
            || (steps.value[stepIndex]?.items.length ?? MAX_ITEMS_PER_STEP) < MAX_ITEMS_PER_STEP,
    }
}
