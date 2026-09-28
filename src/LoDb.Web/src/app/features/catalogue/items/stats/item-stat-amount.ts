import type { ItemStat } from '../../../../core/api/generated/models/item-stat';

const PERCENT_SCALE = 100;
// One decimal is what a percentage of the stat block ever needs (0.075 is 7.5 %).
const PERCENT_DECIMALS = 10;

/** A stat as a reader counts it: flat as is, a fraction as a percentage (0.25 is 25). */
export function itemStatAmount(row: ItemStat): number {
  return row.isPercent
    ? Math.round(row.value * PERCENT_SCALE * PERCENT_DECIMALS) / PERCENT_DECIMALS
    : row.value;
}
