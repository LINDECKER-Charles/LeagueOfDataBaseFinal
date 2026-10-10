import { ITEM_STAT_COLUMNS } from '../../stats/item-stat-columns';
import { itemStatKey } from '../../stats/item-stat-key';

/**
 * URL parameters of the item list's facets: its schema (`itemFacetsOf`) keys its filters on
 * them, and the SSR server answers a list URL carrying any other parameter with a redirect
 * to the URL without it. The stat keys follow `itemStatKey`, `move_speed` and
 * `move_speed_pct` alike.
 */
export const ITEM_FACET_KEYS = {
  tag: 'tag',
  edition: 'edition',
  tier: 'tier',
  map: 'map',
  purchasable: 'purchasable',
  consumable: 'consumable',
  price: 'price',
  stats: ITEM_STAT_COLUMNS.map(itemStatKey),
} as const;
