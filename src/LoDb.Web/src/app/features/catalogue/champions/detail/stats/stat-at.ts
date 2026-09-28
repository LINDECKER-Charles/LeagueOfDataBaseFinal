import { growthFactor } from './growth-factor';
import type { StatRow } from './stat-row';

/** The value of a stat at `level` (1 to 18); the attack speed grows by a share of its base. */
export function statAt(row: StatRow, level: number): number {
  switch (row.kind) {
    case 'flat':
      return row.base + row.growth * growthFactor(level);
    case 'percent':
      return row.base * (1 + (row.growth / 100) * growthFactor(level));
    case 'static':
      return row.base;
  }
}
