import { computed, inject, provide, ref, type ComputedRef, type InjectionKey, type Ref } from 'vue'
import {
    activeFacetCount,
    withChoiceMatchAll,
    withChoiceToggled,
    withRange,
    withSelection,
    withToggle,
    type FacetDefinition,
    type FacetState,
    type RangeSelection,
} from './facets'

/**
 * The engaged facets of a list as reactive state, mutated only through the
 * pure transitions of ./facets so every change yields a fresh object (the
 * watchers fire, the grid repaints, the URL rewrites).
 */
export interface FacetStateStore {
    facets: Ref<FacetState>
    activeCount: ComputedRef<number>
    toggleChoice: (facet: FacetDefinition, value: string) => void
    setMatchAll: (key: string, all: boolean) => void
    setRange: (key: string, range: RangeSelection | null) => void
    setToggle: (key: string, isOn: boolean) => void
    clearFacet: (key: string) => void
    clearAll: () => void
}

export function useFacetState(initial: FacetState): FacetStateStore {
    const facets = ref<FacetState>(initial)

    return {
        facets,
        activeCount: computed(() => activeFacetCount(facets.value)),
        toggleChoice: (facet, value) => (facets.value = withChoiceToggled(facets.value, facet, value)),
        setMatchAll: (key, all) => (facets.value = withChoiceMatchAll(facets.value, key, all)),
        setRange: (key, range) => (facets.value = withRange(facets.value, key, range)),
        setToggle: (key, isOn) => (facets.value = withToggle(facets.value, key, isOn)),
        clearFacet: (key) => (facets.value = withSelection(facets.value, key, undefined)),
        clearAll: () => (facets.value = {}),
    }
}

/*
 * The island owns one store that the rail, the mobile sheet and every facet
 * control mutate. It is provided rather than threaded through three levels of
 * emits: the facet controls are only ever rendered under a ResourceFilter.
 */
const FACET_STORE: InjectionKey<FacetStateStore> = Symbol('facetStore')

export function provideFacetStore(store: FacetStateStore): void {
    provide(FACET_STORE, store)
}

export function injectFacetStore(): FacetStateStore {
    const store = inject(FACET_STORE)
    if (!store) throw new Error('Facet controls must be rendered under a ResourceFilter island.')
    return store
}
