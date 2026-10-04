import type { GameStat } from '../../../../core/api/generated/models/game-stat';

/** The level-1 stats the list filters on, each under its URL key. */
export const CHAMPION_BASE_STATS: readonly { readonly key: string; readonly stat: GameStat }[] = [
  { key: 'hp', stat: 'health' },
  { key: 'armor', stat: 'armor' },
  { key: 'mr', stat: 'magic_resist' },
  { key: 'ad', stat: 'attack_damage' },
  { key: 'as', stat: 'attack_speed' },
  { key: 'ms', stat: 'move_speed' },
];
