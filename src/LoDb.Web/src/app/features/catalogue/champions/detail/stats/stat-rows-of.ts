import type { ChampionCard } from '../../../../../core/api/generated/models/champion-card';
import type { ChampionStat } from '../../../../../core/api/generated/models/champion-stat';
import type { GameStat } from '../../../../../core/api/generated/models/game-stat';
import type { StatKind } from './stat-kind';
import type { StatRow } from './stat-row';

interface RowSpec {
  readonly stat: GameStat;
  readonly label: string;
  readonly kind: StatKind;
}

// Offence, then survival: the order of the champion pages players know.
const LEADING: readonly RowSpec[] = [
  { stat: 'attack_damage', label: 'stat.attack_damage', kind: 'flat' },
  { stat: 'attack_speed', label: 'stat.attack_speed', kind: 'percent' },
  { stat: 'health', label: 'stat.health', kind: 'flat' },
  { stat: 'health_regen', label: 'stat.health_regen', kind: 'flat' },
  { stat: 'armor', label: 'stat.armor', kind: 'flat' },
  { stat: 'magic_resist', label: 'stat.magic_resist', kind: 'flat' },
];
// Named after the champion's own resource (mana, energy, fury...).
const RESOURCE: readonly RowSpec[] = [
  { stat: 'mana', label: 'stat.mana', kind: 'flat' },
  { stat: 'mana_regen', label: 'champion.detail.stats.resource_regen', kind: 'flat' },
];
const TRAILING: readonly RowSpec[] = [
  { stat: 'move_speed', label: 'stat.move_speed', kind: 'static' },
  { stat: 'attack_range', label: 'stat.attack_range', kind: 'static' },
];

function rowOf(spec: RowSpec, stat: ChampionStat | undefined, resource: string): StatRow {
  return {
    stat: spec.stat,
    label: spec.label,
    text: spec.stat === 'mana' ? resource : null,
    base: stat?.base ?? 0,
    growth: spec.kind === 'static' ? 0 : (stat?.perLevel ?? 0),
    kind: spec.kind,
  };
}

/**
 * The rows of a champion's stat board. The resource rows only exist for a champion that has
 * a named resource with a pool: a champion without one (Garen, Katarina) shows none.
 */
export function statRowsOf(profile: ChampionCard): StatRow[] {
  const byStat = new Map(profile.stats.map((stat) => [stat.stat, stat]));
  const resource = profile.partype ?? '';
  const hasResource = resource !== '' && (byStat.get('mana')?.base ?? 0) > 0;
  const specs = [...LEADING, ...(hasResource ? RESOURCE : []), ...TRAILING];
  return specs.map((spec) => rowOf(spec, byStat.get(spec.stat), resource));
}
