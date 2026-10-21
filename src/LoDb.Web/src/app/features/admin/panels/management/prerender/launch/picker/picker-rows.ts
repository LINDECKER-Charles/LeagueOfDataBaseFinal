import type { PrerenderPatchRow } from '../../../../../../../core/api/generated/models/prerender-patch-row';
import { type PatchStatus, patchStatusOf } from '../../common/patches/patch-status-of';
import { type Season, seasonOf } from '../../common/patches/season-of';
import { patchMatches } from '../../common/selection/patch-matches';
import type { PatchSelection } from '../../common/selection/patch-selection';

/** The status filter of the list: every patch, or those of one status (§ 3.3.3). */
export type StatusFilter = 'all' | 'complete' | 'partial' | 'absent' | 'failed';

/** A season heading the patches of one first segment. */
export interface SeasonLine {
  readonly kind: 'season';
  /** `season:16`: never a version. */
  readonly key: string;
  readonly season: Season;
  /** Its versions the filters let through, folded or not: what its box ticks. */
  readonly versions: readonly string[];
  /** A cell per locale over the season: c each patch holds it, - none does, p some. */
  readonly cells: string;
  readonly isOpen: boolean;
  /** How many of `versions` are ticked: its box is ticked, mixed or empty. */
  readonly ticked: number;
}

/** One older patch. */
export interface PatchLine {
  readonly kind: 'patch';
  /** Its version. */
  readonly key: string;
  readonly row: PrerenderPatchRow;
  readonly status: PatchStatus;
  /** The key of its season's line. */
  readonly seasonKey: string;
  readonly isTicked: boolean;
  /** Its rank among the versions rendered first, from 1; 0 when it is not one. */
  readonly rank: number;
}

export type PickerLine = SeasonLine | PatchLine;

/** What the filters and the folds leave of the list. */
export interface PickerFilter {
  readonly query: string;
  readonly status: StatusFilter;
  /** The keys of the seasons open (`16`). */
  readonly open: ReadonlySet<string>;
}

/** The list as shown, and the order a range or a tool works in. */
export interface PickerRows {
  /** The lines shown and reached by the keyboard: seasons, and the patches of open ones. */
  readonly lines: readonly PickerLine[];
  /** Every version the filters let through, folds ignored, newest first. */
  readonly order: readonly string[];
}

const SEASON_PREFIX = 'season:';
const STATUSES: Readonly<Record<StatusFilter, readonly PatchStatus[]>> = {
  all: ['complete', 'complete_with_failures', 'partial', 'absent', 'failed'],
  complete: ['complete'],
  partial: ['partial', 'complete_with_failures'],
  absent: ['absent'],
  failed: ['failed'],
};
const HELD = 'cw';

// Per locale, over the patches of a season: held by each (c), by none (-), by some (p).
function seasonCells(rows: readonly PrerenderPatchRow[]): string {
  const width = rows[0]?.cells.length ?? 0;
  return Array.from({ length: width }, (_, at) => {
    const held = rows.filter((row) => HELD.includes(row.cells[at] ?? '')).length;
    if (held === rows.length) {
      return 'c';
    }
    return held === 0 ? '-' : 'p';
  }).join('');
}

function bySeason(rows: readonly PrerenderPatchRow[]): Map<string, PrerenderPatchRow[]> {
  const seasons = new Map<string, PrerenderPatchRow[]>();
  for (const row of rows) {
    const key = seasonOf(row.version).key;
    const season = seasons.get(key) ?? [];
    season.push(row);
    seasons.set(key, season);
  }
  return seasons;
}

function linesOf(rows: readonly PrerenderPatchRow[], filter: PickerFilter, shown: PatchLine[]) {
  const key = seasonOf(rows[0].version).key;
  const isOpen = filter.open.has(key);
  const season: SeasonLine = {
    kind: 'season',
    key: `${SEASON_PREFIX}${key}`,
    season: seasonOf(rows[0].version),
    versions: shown.map((line) => line.key),
    cells: seasonCells(rows),
    isOpen,
    ticked: shown.filter((line) => line.isTicked).length,
  };
  return [season, ...(isOpen ? shown : [])];
}

// The patches of one season the filters let through.
function shownOf(
  rows: readonly PrerenderPatchRow[],
  filter: PickerFilter,
  selection: PatchSelection,
) {
  const statuses = STATUSES[filter.status];
  const seasonKey = `${SEASON_PREFIX}${seasonOf(rows[0].version).key}`;
  return rows
    .map((row) => ({ row, status: patchStatusOf(row) }))
    .filter(
      ({ row, status }) => statuses.includes(status) && patchMatches(row.version, filter.query),
    )
    .map(({ row, status }): PatchLine => ({
      kind: 'patch',
      key: row.version,
      row,
      status,
      seasonKey,
      isTicked: selection.selected.has(row.version),
      rank: selection.priority.indexOf(row.version) + 1,
    }));
}

/**
 * The lines of the list of patches (§ 3.3.1, § 3.3.3), from the older patches newest first:
 * by season, each patch passing the text and status filters, a season no patch passes left
 * out; the patches of a folded season are not shown, but count in `order`, which a Shift
 * range or a tool works in. Each line says what `selection` ticks of it.
 */
export function pickerRows(
  rows: readonly PrerenderPatchRow[],
  filter: PickerFilter,
  selection: PatchSelection,
): PickerRows {
  const lines: PickerLine[] = [];
  const order: string[] = [];
  for (const patches of bySeason(rows).values()) {
    const shown = shownOf(patches, filter, selection);
    if (shown.length > 0) {
      lines.push(...linesOf(patches, filter, shown));
      order.push(...shown.map((line) => line.key));
    }
  }
  return { lines, order };
}
