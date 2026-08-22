import { onBeforeUnmount, ref, type Ref } from 'vue'
import { normalizeSearchText } from '../search/normalizeSearchText'
import type { CardValues, FacetDefinition, FacetValue } from './facets'
import type { FilterableCard } from './visibleCards'

/**
 * DOM side of the resource filter: the grid stays SERVER-rendered (rich
 * per-type cards), so the island reads it once and then shows/hides cards
 * imperatively. All the querySelector / dataset / inline-style knowledge lives
 * here; the island itself only decides WHICH cards are visible.
 */
const CARD_SELECTOR = ':scope > [data-search]'
const FACET_ATTRIBUTE_PREFIX = 'data-f-'
const LIST_SEPARATOR = '|'

/** A server-rendered card, paired with the element it was read from. */
export interface GridCard extends FilterableCard {
    el: HTMLElement
}

export interface RangeBounds {
    min: number
    max: number
}

/** What the grid actually carries, per facet — the island offers nothing beyond it. */
export interface FacetUniverse {
    /** Choice facets: the tokens present in the grid. */
    present: Record<string, Set<string>>
    /** Range facets: the span of values present. */
    bounds: Record<string, RangeBounds>
    /** Toggle facets: how many cards are flagged. */
    flagged: Record<string, number>
}

export interface CardGrid {
    cards: Ref<GridCard[]>
    universe: Ref<FacetUniverse>
    /** Read the grid; a no-op when no element carries the configured id. */
    scan: () => void
    /** Show exactly these cards and hide every other one. */
    show: (visible: readonly GridCard[]) => void
    /**
     * Hand paint control over to JS: the pre-hide CSS (first page only, the
     * no-JS fallback) steps aside so the inline styles drive pagination.
     */
    markReady: () => void
}

interface GridState {
    grid: Ref<HTMLElement | null>
    cards: Ref<GridCard[]>
    universe: Ref<FacetUniverse>
}

export function useCardGrid(gridId: string, schema: readonly FacetDefinition[]): CardGrid {
    const state: GridState = {
        grid: ref<HTMLElement | null>(null),
        cards: ref<GridCard[]>([]),
        universe: ref<FacetUniverse>(emptyUniverse()),
    }

    // Leave the server-rendered grid exactly as it was found: a Turbo visit
    // reuses the same DOM, and stale inline styles would hide real cards.
    onBeforeUnmount(() => {
        state.cards.value.forEach((card) => (card.el.style.display = ''))
        state.grid.value?.removeAttribute('data-ready')
    })

    return {
        cards: state.cards,
        universe: state.universe,
        scan: () => scanGrid(state, gridId, schema),
        show: (visible) => showOnly(state.cards.value, visible),
        markReady: () => markGridReady(state.grid.value),
    }
}

function emptyUniverse(): FacetUniverse {
    return { present: {}, bounds: {}, flagged: {} }
}

function scanGrid(state: GridState, gridId: string, schema: readonly FacetDefinition[]): void {
    const el = document.getElementById(gridId)
    if (!el) return
    const kinds = new Map(schema.map((facet) => [facet.key, facet.kind]))
    state.grid.value = el
    state.cards.value = Array.from(el.querySelectorAll<HTMLElement>(CARD_SELECTOR))
        .map((card) => readCard(card, kinds))
    state.universe.value = collectUniverse(state.cards.value)
}

function showOnly(cards: readonly GridCard[], visible: readonly GridCard[]): void {
    const shown = new Set(visible.map((card) => card.el))
    for (const card of cards) {
        card.el.style.display = shown.has(card.el) ? '' : 'none'
    }
}

function markGridReady(grid: HTMLElement | null): void {
    if (grid) {
        grid.dataset.ready = 'true'
    }
}

function readCard(el: HTMLElement, kinds: ReadonlyMap<string, FacetDefinition['kind']>): GridCard {
    const values: CardValues = {}
    for (const attribute of Array.from(el.attributes)) {
        if (!attribute.name.startsWith(FACET_ATTRIBUTE_PREFIX)) continue
        const key = attribute.name.slice(FACET_ATTRIBUTE_PREFIX.length)
        const value = parseValue(attribute.value, kinds.get(key))
        if (value !== undefined) values[key] = value
    }
    return {
        el,
        // Accent-folded like the pickers, so the needle and haystack agree.
        search: normalizeSearchText(el.dataset.search ?? ''),
        values,
    }
}

/** A facet the schema does not know is ignored: the markup is never trusted over it. */
function parseValue(raw: string, kind: FacetDefinition['kind'] | undefined): FacetValue | undefined {
    switch (kind) {
        case 'choice':
            return raw.split(LIST_SEPARATOR).filter(Boolean)
        case 'range': {
            const number = Number(raw)
            return Number.isFinite(number) ? number : undefined
        }
        case 'toggle':
            return raw === '1' ? true : undefined
        default:
            return undefined
    }
}

function collectUniverse(cards: readonly GridCard[]): FacetUniverse {
    const universe = emptyUniverse()
    for (const card of cards) {
        for (const [key, value] of Object.entries(card.values)) {
            if (Array.isArray(value)) {
                const present = (universe.present[key] ??= new Set())
                value.forEach((token) => present.add(token))
            } else if (value === true) {
                universe.flagged[key] = (universe.flagged[key] ?? 0) + 1
            } else {
                const bounds = universe.bounds[key]
                universe.bounds[key] = bounds
                    ? { min: Math.min(bounds.min, value), max: Math.max(bounds.max, value) }
                    : { min: value, max: value }
            }
        }
    }
    return universe
}
