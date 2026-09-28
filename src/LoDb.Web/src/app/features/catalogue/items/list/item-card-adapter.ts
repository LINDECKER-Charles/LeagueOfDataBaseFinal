import type { ItemCard } from '../../../../core/api/generated/models/item-card';
import type { CardValues } from '../../shared/facets/model/card-values';
import type { FacetValue } from '../../shared/facets/model/facet-value';
import type { CatalogueCardAdapter } from '../../shared/state/catalogue-card-adapter';
import { itemStatAmount } from '../stats/item-stat-amount';
import { itemStatKey } from '../stats/item-stat-key';

function flagsOf(card: ItemCard): Record<string, FacetValue> {
  const flags: Record<string, FacetValue> = {};
  if (card.gold.isPurchasable) {
    flags['purchasable'] = true;
  }
  if (card.consumable) {
    flags['consumable'] = true;
  }
  if (card.tier) {
    flags['tier'] = [card.tier];
  }
  return flags;
}

// A stat facet only sees the items carrying the stat: "armor ≥ 0" must not match a trinket.
function statsOf(card: ItemCard): Record<string, FacetValue> {
  return Object.fromEntries(card.stats.map((row) => [itemStatKey(row), itemStatAmount(row)]));
}

/**
 * How the item list filters its cards, by id: the search reads the name and the id, as the
 * legacy list did; the facets read the tags, the edition, the maps, the tier, the flags,
 * the price and each stat.
 */
export const ITEM_CARD_ADAPTER: CatalogueCardAdapter<ItemCard> = {
  searchTextOf: (card) => `${card.name} ${card.id}`,
  valuesOf: (card): CardValues => ({
    tag: card.tags,
    edition: [card.edition],
    map: card.maps.map(String),
    price: card.gold.total,
    ...flagsOf(card),
    ...statsOf(card),
  }),
  keyOf: (card) => card.id,
};
