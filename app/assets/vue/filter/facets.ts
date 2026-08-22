/**
 * Facet model of the resource grids — the client half of
 * App\Service\Catalog\Facet. The schema says how each facet is presented and
 * combined; a card's values ride on its `data-f-<key>` attributes; the state
 * holds only the facets the reader has engaged. Pure: no Vue, no DOM.
 */
export type FacetKind = 'choice' | 'range' | 'toggle'

export interface FacetOption {
    value: string
    label: string
}

export interface FacetDefinition {
    /** Stable, locale-independent — it is the URL parameter too. */
    key: string
    kind: FacetKind
    label: string
    group: string
    /** Known values in display order; only those present in the grid are offered. */
    options: FacetOption[]
    /** A main axis of the list: its group starts unfolded. */
    primary: boolean
    /** Choice: several values (OR) rather than exactly one. */
    multiple: boolean
    /** Choice: offers the any/all switch. */
    matchAll: boolean
    unit: string | null
    step: number
}

/** Tokens for a choice, a number for a range, `true` for a set toggle. */
export type FacetValue = string[] | number | true
export type CardValues = Record<string, FacetValue>

export interface ChoiceSelection {
    values: string[]
    /** Every selected value must be carried by the card (instead of any of them). */
    all: boolean
}

export interface RangeSelection {
    min: number
    max: number
}

export type FacetSelection = ChoiceSelection | RangeSelection | true

/** Only engaged facets are present; an absent key filters nothing. */
export type FacetState = Record<string, FacetSelection>

export function isChoiceSelection(selection: FacetSelection): selection is ChoiceSelection {
    return typeof selection === 'object' && 'values' in selection
}

export function isRangeSelection(selection: FacetSelection): selection is RangeSelection {
    return typeof selection === 'object' && 'min' in selection
}

export function matchesFacets(
    values: CardValues,
    state: FacetState,
    schema: readonly FacetDefinition[],
): boolean {
    for (const facet of schema) {
        const selection = state[facet.key]
        if (selection === undefined) continue
        if (!matchesFacet(values[facet.key], selection)) return false
    }
    return true
}

function matchesFacet(value: FacetValue | undefined, selection: FacetSelection): boolean {
    if (selection === true) {
        return value === true
    }
    if (isRangeSelection(selection)) {
        return typeof value === 'number' && value >= selection.min && value <= selection.max
    }
    if (selection.values.length === 0) return true
    if (!Array.isArray(value)) return false
    return selection.all
        ? selection.values.every((wanted) => value.includes(wanted))
        : selection.values.some((wanted) => value.includes(wanted))
}

/**
 * How many facets the state engages — the badge of the console and of the
 * mobile trigger, which therefore sums the badges of the groups.
 */
export function activeFacetCount(state: FacetState): number {
    return Object.keys(state).length
}

/** The state with one value of a choice facet flipped; empty choices vanish. */
export function withChoiceToggled(
    state: FacetState,
    facet: FacetDefinition,
    value: string,
): FacetState {
    const current = state[facet.key]
    const selection: ChoiceSelection = current && isChoiceSelection(current)
        ? current
        : { values: [], all: false }
    const values = selection.values.includes(value)
        ? selection.values.filter((v) => v !== value)
        : facet.multiple ? [...selection.values, value] : [value]
    return withSelection(state, facet.key, values.length ? { ...selection, values } : undefined)
}

export function withChoiceMatchAll(state: FacetState, key: string, all: boolean): FacetState {
    const current = state[key]
    if (!current || !isChoiceSelection(current)) return state
    return withSelection(state, key, { ...current, all })
}

export function withRange(state: FacetState, key: string, range: RangeSelection | null): FacetState {
    return withSelection(state, key, range ?? undefined)
}

export function withToggle(state: FacetState, key: string, isOn: boolean): FacetState {
    return withSelection(state, key, isOn ? true : undefined)
}

export function withSelection(
    state: FacetState,
    key: string,
    selection: FacetSelection | undefined,
): FacetState {
    const next = { ...state }
    if (selection === undefined) {
        delete next[key]
    } else {
        next[key] = selection
    }
    return next
}

/** The facets of a list under their heading, in schema order. */
export interface FacetGroup {
    name: string
    facets: FacetDefinition[]
}

export function groupFacets(facets: readonly FacetDefinition[]): FacetGroup[] {
    const byGroup = new Map<string, FacetDefinition[]>()
    for (const facet of facets) {
        byGroup.set(facet.group, [...(byGroup.get(facet.group) ?? []), facet])
    }
    return [...byGroup].map(([name, group]) => ({ name, facets: group }))
}

/** How many of these facets the state engages — the badge on a group heading. */
export function countEngaged(facets: readonly FacetDefinition[], state: FacetState): number {
    return facets.filter((facet) => state[facet.key] !== undefined).length
}

/**
 * A group unfolds on its own when it carries a primary facet (the list's main
 * axes) or something already engaged (a shared link must show what filters).
 */
export function isGroupOpenByDefault(group: FacetGroup, state: FacetState): boolean {
    return group.facets.some((facet) => facet.primary) || countEngaged(group.facets, state) > 0
}
