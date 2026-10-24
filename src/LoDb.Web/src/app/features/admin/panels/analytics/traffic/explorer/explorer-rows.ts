import type { AnalyticsRank } from '../../../../../../core/api/generated/models/analytics-rank';
import type { AdminText } from '../../../../shared/inject-admin-text';

/** The breakdowns the explorer walks through. */
export type ExplorerDimension = 'pages' | 'entities' | 'referers';

/** A key as the explorer lists it: its name, what kind of thing it is, its views. */
export interface ExplorerRow {
  readonly key: string;
  readonly name: string;
  /** The type of an entity (Champions…); '' for a page or a host. */
  readonly kind: string;
  readonly count: number;
  readonly pct: number;
}

// An entity is counted as `{type}:{key}`.
const ENTITY_SEPARATOR = ':';

function entityRow(rank: AnalyticsRank, texts: AdminText): ExplorerRow {
  const separator = rank.name.indexOf(ENTITY_SEPARATOR);
  const type = separator < 0 ? '' : rank.name.slice(0, separator);
  return {
    key: rank.name,
    name: separator < 0 ? rank.name : rank.name.slice(separator + 1),
    kind: type ? texts.term('traffic.types', type) : '',
    count: rank.count,
    pct: rank.pct,
  };
}

/** The keys of a page of a breakdown, an entity named by its key and typed apart. */
export function explorerRows(
  dimension: ExplorerDimension,
  ranks: readonly AnalyticsRank[],
  texts: AdminText,
): readonly ExplorerRow[] {
  return ranks.map((rank) =>
    dimension === 'entities'
      ? entityRow(rank, texts)
      : { key: rank.name, name: rank.name, kind: '', count: rank.count, pct: rank.pct },
  );
}
