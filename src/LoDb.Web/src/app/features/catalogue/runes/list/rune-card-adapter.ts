import type { RuneCard } from '../../../../core/api/generated/models/rune-card';
import type { CatalogueCardAdapter } from '../../shared/state/catalogue-card-adapter';

// The search reads what the card shows, not Riot's markup.
const TAG = /<[^>]*>/g;

/**
 * How the rune list filters its cards, by id: the search reads the name, the key and the
 * summary, as the legacy list did; the facets read the path and the row.
 */
export const RUNE_CARD_ADAPTER: CatalogueCardAdapter<RuneCard> = {
  searchTextOf: (card) => `${card.name} ${card.key} ${card.shortDesc.replace(TAG, ' ')}`,
  valuesOf: (card) => ({ path: [card.tree], slot: [card.slot] }),
  keyOf: (card) => String(card.id),
};
