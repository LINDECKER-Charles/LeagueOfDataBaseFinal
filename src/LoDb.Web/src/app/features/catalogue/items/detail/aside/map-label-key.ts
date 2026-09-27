import { ITEM_MAPS } from '../../list/facets/item-maps';

/** The LoL Classic Rift: the one map a Classic item claims, and never a filter. */
const CLASSIC_RIFT = 453;
/** Howling Abyss: its facet label names the map, the aside's chip only the mode. */
const HOWLING_ABYSS = 12;

/**
 * The translation key naming a map id of an item's availability line. The facet labels
 * (`map.<id>`) serve, except for ARAM: its long label would wrap the chips of the narrow
 * aside, which the legacy page kept on one line with the bare acronym.
 */
export function mapLabelKey(id: number): string {
  if (id === HOWLING_ABYSS) {
    return 'items.maps.aram';
  }
  if (ITEM_MAPS.includes(id)) {
    return `map.${id}`;
  }
  return id === CLASSIC_RIFT ? 'items.maps.classic_rift' : 'items.maps.other';
}
