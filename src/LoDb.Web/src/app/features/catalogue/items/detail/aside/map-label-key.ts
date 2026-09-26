import { ITEM_MAPS } from '../../list/facets/item-maps';

/** The LoL Classic Rift: the one map a Classic item claims, and never a filter. */
const CLASSIC_RIFT = 453;

/** The translation key naming a map id of an item's availability line. */
export function mapLabelKey(id: number): string {
  if (ITEM_MAPS.includes(id)) {
    return `map.${id}`;
  }
  return id === CLASSIC_RIFT ? 'items.maps.classic_rift' : 'items.maps.other';
}
