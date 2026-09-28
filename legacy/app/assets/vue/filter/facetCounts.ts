import {
    isChoiceSelection,
    withSelection,
    type FacetDefinition,
    type FacetState,
} from './facets'
import { matchingCards, type FilterableCard, type FilterCriteria } from './visibleCards'

/**
 * Per-option result counts, the faceted-search convention: how many cards
 * carry each value while EVERY OTHER engaged facet (and the search) still
 * applies. The facet's own selection is lifted so an unselected value reads
 * "what you would get", not "what is left". One exception keeps the number
 * honest: in match-all mode the selected values stay, since a further value
 * can only narrow the set. Pure: no Vue, no DOM.
 */
export interface FacetCounts {
    /** Choice facets: value → count. */
    options: Record<string, ReadonlyMap<string, number>>
    /** Toggle facets: how many cards in context carry the flag. */
    flagged: Record<string, number>
}

export function countFacetOptions(
    cards: readonly FilterableCard[],
    criteria: FilterCriteria,
): FacetCounts {
    const counts: FacetCounts = { options: {}, flagged: {} }
    for (const facet of criteria.schema) {
        if (facet.kind === 'range') continue
        const context = matchingCards(cards, {
            ...criteria,
            facets: contextFor(criteria.facets, facet),
        })
        if (facet.kind === 'choice') {
            counts.options[facet.key] = tallyTokens(context, facet.key)
        } else {
            counts.flagged[facet.key] = context.filter((card) => card.values[facet.key] === true).length
        }
    }
    return counts
}

/** The engaged facets minus this one — unless it is a match-all choice, which only narrows. */
function contextFor(state: FacetState, facet: FacetDefinition): FacetState {
    const selection = state[facet.key]
    if (selection !== undefined && isChoiceSelection(selection) && selection.all) return state
    return withSelection(state, facet.key, undefined)
}

function tallyTokens(cards: readonly FilterableCard[], key: string): Map<string, number> {
    const tally = new Map<string, number>()
    for (const card of cards) {
        const value = card.values[key]
        if (!Array.isArray(value)) continue
        for (const token of value) {
            tally.set(token, (tally.get(token) ?? 0) + 1)
        }
    }
    return tally
}
