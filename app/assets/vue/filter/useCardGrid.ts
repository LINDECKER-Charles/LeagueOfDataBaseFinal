import { onBeforeUnmount, ref, type Ref } from 'vue'
import type { FilterableCard } from './visibleCards'

/**
 * DOM side of the resource filter: the grid stays SERVER-rendered (rich
 * per-type cards), so the island reads it once and then shows/hides cards
 * imperatively. All the querySelector / dataset / inline-style knowledge lives
 * here; the island itself only decides WHICH cards are visible.
 */

/** A server-rendered card, paired with the element it was read from. */
export interface GridCard extends FilterableCard {
    el: HTMLElement
}

export interface CardGrid {
    cards: Ref<GridCard[]>
    /** Every facet tag present in the grid, alphabetically sorted. */
    tags: Ref<string[]>
    /** Every edition present in the grid, in order of first appearance. */
    editions: Ref<string[]>
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

/** The grid element once found, plus what was read out of it. */
interface GridState {
    grid: Ref<HTMLElement | null>
    cards: Ref<GridCard[]>
    tags: Ref<string[]>
    editions: Ref<string[]>
}

export function useCardGrid(gridId: string): CardGrid {
    const state: GridState = {
        grid: ref<HTMLElement | null>(null),
        cards: ref<GridCard[]>([]),
        tags: ref<string[]>([]),
        editions: ref<string[]>([]),
    }

    // Leave the server-rendered grid exactly as it was found: a Turbo visit
    // reuses the same DOM, and stale inline styles would hide real cards.
    onBeforeUnmount(() => {
        state.cards.value.forEach((card) => (card.el.style.display = ''))
        state.grid.value?.removeAttribute('data-ready')
    })

    return {
        cards: state.cards,
        tags: state.tags,
        editions: state.editions,
        scan: () => scanGrid(state, gridId),
        show: (visible) => showOnly(state.cards.value, visible),
        markReady: () => markGridReady(state.grid.value),
    }
}

function scanGrid(state: GridState, gridId: string): void {
    const el = document.getElementById(gridId)
    if (!el) return
    state.grid.value = el
    state.cards.value = Array.from(
        el.querySelectorAll<HTMLElement>(':scope > [data-search]'),
    ).map(readCard)
    state.tags.value = collectTags(state.cards.value)
    state.editions.value = collectEditions(state.cards.value)
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

function readCard(el: HTMLElement): GridCard {
    const card: GridCard = {
        el,
        search: (el.dataset.search ?? '').toLowerCase(),
        tags: (el.dataset.tags ?? '').split('|').filter(Boolean),
    }
    if (el.dataset.edition) card.edition = el.dataset.edition
    return card
}

function collectTags(cards: readonly GridCard[]): string[] {
    const universe = new Set<string>()
    cards.forEach((card) => card.tags.forEach((tag) => universe.add(tag)))
    return Array.from(universe).sort((a, b) => a.localeCompare(b))
}

function collectEditions(cards: readonly GridCard[]): string[] {
    const universe = new Set<string>()
    cards.forEach((card) => card.edition && universe.add(card.edition))
    return Array.from(universe)
}
