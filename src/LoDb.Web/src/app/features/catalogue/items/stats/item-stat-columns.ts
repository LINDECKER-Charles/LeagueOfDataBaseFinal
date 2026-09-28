import type { GameStat } from '../../../../core/api/generated/models/game-stat';

/** A stat an item's stat block may carry: flat, or a fraction read as a percentage. */
export interface ItemStatColumn {
  readonly stat: GameStat;
  readonly isPercent: boolean;
}

/**
 * The item stats the list filters on, in display order: the twelve classic keys of Data
 * Dragon's stat block, as the API reads them (move speed comes flat and as a percentage).
 */
export const ITEM_STAT_COLUMNS: readonly ItemStatColumn[] = [
  { stat: 'attack_damage', isPercent: false },
  { stat: 'ability_power', isPercent: false },
  { stat: 'attack_speed', isPercent: true },
  { stat: 'crit_chance', isPercent: true },
  { stat: 'life_steal', isPercent: true },
  { stat: 'health', isPercent: false },
  { stat: 'health_regen', isPercent: false },
  { stat: 'armor', isPercent: false },
  { stat: 'magic_resist', isPercent: false },
  { stat: 'mana', isPercent: false },
  { stat: 'move_speed', isPercent: false },
  { stat: 'move_speed', isPercent: true },
];
