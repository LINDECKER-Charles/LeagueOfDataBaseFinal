import type { ChampionCard } from '../../../../core/api/generated/models/champion-card';
import type { FacetOption } from '../../shared/facets/model/facet-option';

/** The token of the champions that spend no resource. */
const NO_RESOURCE = 'none';

/**
 * The resources the cards carry, most common first (mana, then energy...), each labelled
 * as the catalog's language names it; `none` under the label given for it.
 */
export function resourceOptions(cards: readonly ChampionCard[], noneLabel: string): FacetOption[] {
  const counts = new Map<string, number>();
  const labels = new Map<string, string>();
  for (const card of cards) {
    counts.set(card.resource, (counts.get(card.resource) ?? 0) + 1);
    if (!labels.has(card.resource)) {
      const named = card.partype?.trim() || card.resource;
      labels.set(card.resource, card.resource === NO_RESOURCE ? noneLabel : named);
    }
  }
  return [...counts.entries()]
    .sort(([, a], [, b]) => b - a)
    .map(([value]) => ({ value, label: labels.get(value) ?? value }));
}
