import type { GameStat } from '../../../../../core/api/generated/models/game-stat';

/**
 * The stats that have artwork, CommunityDragon's rune-shard icons under public/icons/stats.
 * Mana, the regenerations, the range, crit and life steal have none, as in the legacy
 * GameStat: their label stands alone.
 */
const WITH_ICON: ReadonlySet<GameStat> = new Set<GameStat>([
  'attack_damage',
  'ability_power',
  'attack_speed',
  'health',
  'armor',
  'magic_resist',
  'move_speed',
]);

/** The public path of a stat's icon, or null for a stat without artwork. */
export function statIconOf(stat: GameStat): string | null {
  return WITH_ICON.has(stat) ? `/icons/stats/${stat}.png` : null;
}
