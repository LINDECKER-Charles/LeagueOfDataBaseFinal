import type { ItemStatColumn } from './item-stat-columns';

/** Suffix of a percentage stat's facet, which the flat one of the same stat does not carry. */
const PERCENT_SUFFIX = '_pct';

/** The facet key, and URL parameter, of an item stat: `armor`, `move_speed_pct`. */
export function itemStatKey(column: ItemStatColumn): string {
  return column.isPercent ? `${column.stat}${PERCENT_SUFFIX}` : column.stat;
}
