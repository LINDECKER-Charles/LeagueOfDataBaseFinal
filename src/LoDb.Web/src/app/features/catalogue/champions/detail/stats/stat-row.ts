import type { GameStat } from '../../../../../core/api/generated/models/game-stat';
import type { StatKind } from './stat-kind';

/** One line of the stat board: its label, its level-1 value and how it grows. */
export interface StatRow {
  readonly stat: GameStat;
  /** Translation key of the label. */
  readonly label: string;
  /** The label as the API writes it, when it has one: the champion's own resource. */
  readonly text: string | null;
  readonly base: number;
  /** Gain per level: flat, or a percentage of the base when the kind is `percent`. */
  readonly growth: number;
  readonly kind: StatKind;
}
