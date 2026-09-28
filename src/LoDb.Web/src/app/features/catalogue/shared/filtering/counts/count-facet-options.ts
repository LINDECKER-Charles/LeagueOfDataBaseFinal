import type { FacetDefinition } from '../../facets/model/facet-definition';
import type { FacetState } from '../../facets/model/facet-state';
import { isChoiceSelection } from '../../facets/rules/is-choice-selection';
import { withSelection } from '../../facets/transitions/with-selection';
import type { FilterCriteria } from '../grid/filter-criteria';
import type { FilterableCard } from '../grid/filterable-card';
import { matchingCards } from '../grid/matching-cards';
import type { FacetCounts } from './facet-counts';

// The engaged facets minus this one, unless it is a match-all choice: there a further value
// can only narrow the set, so the selection stays and the number stays honest.
function contextFor(state: FacetState, facet: FacetDefinition): FacetState {
  const selection = state[facet.key];
  if (selection !== undefined && isChoiceSelection(selection) && selection.all) {
    return state;
  }
  return withSelection(state, facet.key, undefined);
}

function tallyTokens(cards: readonly FilterableCard[], key: string): Map<string, number> {
  const tally = new Map<string, number>();
  for (const card of cards) {
    const value = card.values[key];
    if (Array.isArray(value)) {
      const tokens: readonly string[] = value;
      tokens.forEach((token) => tally.set(token, (tally.get(token) ?? 0) + 1));
    }
  }
  return tally;
}

/**
 * The faceted-search convention: a value is counted under every OTHER engaged axis, its own
 * facet's selection lifted, so an unpicked value reads "what you would get", not "what is
 * left". Ranges carry no count.
 */
export function countFacetOptions(
  cards: readonly FilterableCard[],
  criteria: FilterCriteria,
): FacetCounts {
  const options: Record<string, ReadonlyMap<string, number>> = {};
  const flagged: Record<string, number> = {};
  for (const facet of criteria.schema) {
    if (facet.kind === 'range') {
      continue;
    }
    const context = matchingCards(cards, {
      ...criteria,
      facets: contextFor(criteria.facets, facet),
    });
    if (facet.kind === 'choice') {
      options[facet.key] = tallyTokens(context, facet.key);
    } else {
      flagged[facet.key] = context.filter((card) => card.values[facet.key] === true).length;
    }
  }
  return { options, flagged };
}
