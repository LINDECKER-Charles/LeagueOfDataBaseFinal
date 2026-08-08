import { computed, nextTick, ref, type ComputedRef, type Ref } from 'vue'
import type { BuildEditorLabels } from './editorLabels'
import { draftRunes, type RuneDraft } from '../runes/runeRules'
import { parseStructure, serializeStructure, type BuildStep } from './structure'
import { useEditorCatalogs } from './useEditorCatalogs'
import { useRuneDraft } from '../runes/useRuneDraft'
import { useStepEditing } from '../steps/useStepEditing'

export type { GhostReason } from '../items/useItemIdentity'

export interface GameModeOption {
    value: string
    label: string
}

export interface LanguageOption {
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
    /** Initially selected authoring locale (Data Dragon code, e.g. "fr_FR"). */
    language: string
    /** Authoring locales offered by the language select. */
    languages: LanguageOption[]
    labels: BuildEditorLabels
}

/**
 * Orchestrates the editor island: parses the initial structure, then assembles
 * the three units it is made of — the catalogs bound to the live (version,
 * mode) context, the rune draft and the purchase order — and keeps the
 * serialized `structure` JSON in sync for the hidden form input.
 */
export function useBuildEditor(props: BuildEditorProps) {
    const gameVersion = ref(props.version)
    const gameMode = ref(props.gameMode)
    // Authoring locale is pure metadata (the free text's language) — it feeds the
    // hidden form field only and, unlike version/mode, never reloads any catalog.
    const language = ref(props.language)
    const { announcement, announce } = useAnnouncer()

    const catalogs = useEditorCatalogs(props, gameVersion, gameMode)
    const initial = parseStructure(props.initial)
    const championId = ref(initial?.championId ?? '')
    const draft = useRuneDraft(initial?.runes ?? null, catalogs.runes.data)
    const order = useStepEditing(initial?.steps ?? [], announce, props.labels.dnd)

    return {
        ...catalogs,
        ...draft,
        ...order,
        gameVersion,
        gameMode,
        language,
        championId,
        announcement,
        structureJson: useStructureJson(championId, draft.runeDraft, order.steps),
        setChampion: (id: string) => void (championId.value = id),
    }
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

/** Serialized mirror of the whole selection, read by the hidden form input. */
function useStructureJson(
    championId: Ref<string>,
    runeDraft: Ref<RuneDraft>,
    steps: Ref<BuildStep[]>,
): ComputedRef<string> {
    return computed(() => serializeStructure({
        championId: championId.value,
        runes: draftRunes(runeDraft.value),
        steps: steps.value,
    }))
}
