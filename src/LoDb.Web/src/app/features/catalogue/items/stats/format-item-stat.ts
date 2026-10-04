import type { ItemStat } from '../../../../core/api/generated/models/item-stat';
import { itemStatAmount } from './item-stat-amount';

/** A stat as its row shows it, signed: `+75`, `+25 %`. */
export function formatItemStat(row: ItemStat): string {
  const amount = itemStatAmount(row);
  return `+${amount}${row.isPercent ? ' %' : ''}`;
}
