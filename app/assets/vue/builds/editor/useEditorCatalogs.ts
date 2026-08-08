import { ref, watch, type Ref } from 'vue'
import {
    championOptions,
    itemOptions,
    runeTrees,
    type ChampionOption,
    type ItemOption,
    type RuneTree,
} from '../catalog/catalogTypes'
import { usePickerCatalog, type PickerCatalog } from '../catalog/usePickerCatalog'
import { useItemIdentity, type ItemIdentity } from '../items/useItemIdentity'
import { BUILD_WARM_PATH, requestWarm } from '../../loader/warmBridge'

/** The slice of the island props the catalogs need to build their URLs. */
export interface CatalogContext {
    endpoints: { champions: string; items: string; runes: string }
    lang: string
    /** Patch selected on mount — already warm, so its load is never gated. */
    version: string
}

interface CatalogTrio {
    champions: PickerCatalog<ChampionOption[]>
    items: PickerCatalog<ItemOption[]>
    runes: PickerCatalog<RuneTree[]>
}

export interface EditorCatalogs extends CatalogTrio, ItemIdentity {
    loadCatalogs: () => Promise<unknown>
}

/**
 * Catalog trio bound to the LIVE (version, mode) context. Version switches
 * reload all three; mode switches only reload items (the sole mode-scoped
 * dataset).
 */
export function useEditorCatalogs(
    context: CatalogContext,
    gameVersion: Ref<string>,
    gameMode: Ref<string>,
): EditorCatalogs {
    const query = (endpoint: string, extra = ''): string =>
        `${endpoint}?version=${encodeURIComponent(gameVersion.value)}`
        + `&lang=${encodeURIComponent(context.lang)}${extra}`

    const champions = usePickerCatalog(() => query(context.endpoints.champions), championOptions)
    const items = usePickerCatalog(
        () => query(context.endpoints.items, `&mode=${encodeURIComponent(gameMode.value)}`),
        itemOptions,
    )
    const runes = usePickerCatalog(() => query(context.endpoints.runes), runeTrees)

    watchPatchSwitch({ champions, items, runes }, context, gameVersion)
    watch(gameMode, () => void items.reload())

    return {
        champions,
        items,
        runes,
        loadCatalogs: () => Promise.all([champions.load(), items.load(), runes.load()]),
        ...useItemIdentity(items.data, rememberItems(items.data)),
    }
}

/**
 * Switching to a patch whose images aren't warm yet loads a full catalog set:
 * gate it behind the global loader (real SSE progress over the ingestion) so
 * the reloaded pickers land on real icons, not placeholders. The initial patch
 * loaded un-gated on mount, and each warmed patch is only gated once a session.
 */
function watchPatchSwitch(trio: CatalogTrio, context: CatalogContext, patch: Ref<string>): void {
    const warmedPatches = new Set<string>([context.version])

    async function reloadOn(version: string): Promise<void> {
        if (!warmedPatches.has(version)) {
            await requestWarm(version, context.lang, BUILD_WARM_PATH)
            warmedPatches.add(version)
        }
        await Promise.all([trio.champions.reload(), trio.items.reload(), trio.runes.reload()])
    }

    watch(patch, (version) => void reloadOn(version))
}

/** Every item option seen this session, indexed by id (see useItemIdentity). */
function rememberItems(options: Ref<ItemOption[] | null>): Ref<Record<string, ItemOption>> {
    const known = ref<Record<string, ItemOption>>({})

    watch(options, (list) => {
        if (!list) return
        const merged = { ...known.value }
        for (const option of list) merged[option.id] = option
        known.value = merged
    })

    return known
}
