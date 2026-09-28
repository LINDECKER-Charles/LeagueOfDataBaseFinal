import type { AnalyticsRank } from '../../../../core/api/generated/models/analytics-rank';
import type { RankRow } from '../../widgets/rank-list';

/** The ranked counts of a report as rows, their names written by `name` (as they are by default). */
export function rankRows(
  ranks: readonly AnalyticsRank[],
  name: (raw: string) => string = (raw) => raw,
): RankRow[] {
  return ranks.map((rank) => ({ name: name(rank.name), value: rank.count }));
}
