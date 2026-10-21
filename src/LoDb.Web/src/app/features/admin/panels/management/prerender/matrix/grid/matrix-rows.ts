import { seasonOf } from '../../common/patches/season-of';
import type { CellMode } from './cell-state';
import type { MatrixVersion } from './unit-matrix';

/** Where a version stands as a whole, an `admin.prerender.common.cells.*` key. */
export type VersionStatus = 'running' | 'failed' | 'complete' | 'pending';

/** A version of the matrix: its cells one by one, and their counts. */
export interface MatrixRow {
  readonly version: MatrixVersion;
  readonly codes: readonly string[];
  readonly status: VersionStatus;
  /** Locales complete, with failed pages or not. */
  readonly complete: number;
  /** Locales the version has a unit in. */
  readonly total: number;
  readonly running: number;
  readonly failed: number;
}

/** A column of a season's summary row: how much of a locale is complete, and its signals. */
export interface SeasonColumn {
  readonly share: number;
  readonly isRunning: boolean;
  readonly hasFailed: boolean;
}

/** A season of the matrix, folded or not, with its versions counted by status. */
export interface MatrixSeason {
  /** The first segment of its versions. */
  readonly key: string;
  readonly major: number | null;
  readonly year: number | null;
  /** Whether it is the single group of the pass's order rather than a season. */
  readonly isRanked: boolean;
  readonly isOpen: boolean;
  readonly rows: readonly MatrixRow[];
  readonly counts: Readonly<Record<VersionStatus, number>> & { readonly versions: number };
  readonly columns: readonly SeasonColumn[];
}

/** How the matrix lays its rows out. */
export interface MatrixRowsOptions {
  readonly mode: CellMode;
  /** The seasons the operator opened (true) or folded (false); the rest follow the default. */
  readonly folds: ReadonlyMap<string, boolean>;
  /** One group in the order the worker reaches the versions, rather than by season. */
  readonly byRank?: boolean;
  /** The version of the next unit, whose season opens by default. */
  readonly next?: string | null;
}

// The key of the single group of the pass's order, which no season shares.
const RANK_GROUP = 'rank';
// A pass of 40 versions or fewer fits unfolded; past that, only the seasons where it acts.
const UNFOLDED_VERSIONS = 40;
const CODES: Readonly<Record<CellMode, Readonly<Record<string, string>>>> = {
  pass: { running: 'rz', failed: 'fw', clean: 'c', active: 'rzfw' },
  coverage: { running: '', failed: 'f', clean: 'cw', active: '-pfw' },
};
const COMPLETE = 'cw';
const NO_UNIT = '_';

function countOf(codes: readonly string[], set: string): number {
  return codes.filter((code) => set.includes(code)).length;
}

function rowOf(version: MatrixVersion, mode: CellMode): MatrixRow {
  const codes = [...version.cells];
  const sets = CODES[mode];
  const total = codes.filter((code) => code !== NO_UNIT).length;
  const running = countOf(codes, sets['running']);
  const failed = countOf(codes, sets['failed']);
  let status: VersionStatus = 'pending';
  if (running > 0) {
    status = 'running';
  } else if (failed > 0) {
    status = 'failed';
  } else if (total > 0 && countOf(codes, sets['clean']) === total) {
    status = 'complete';
  }
  return { version, codes, status, complete: countOf(codes, COMPLETE), total, running, failed };
}

// Newest first, segment by segment: 16.10.1 before 16.9.1.
function newestFirst(left: MatrixRow, right: MatrixRow): number {
  const a = left.version.version.split('.').map(Number);
  const b = right.version.version.split('.').map(Number);
  for (let index = 0; index < Math.max(a.length, b.length); index++) {
    const gap = (b[index] ?? 0) - (a[index] ?? 0);
    if (gap !== 0) {
      return gap;
    }
  }
  return 0;
}

function columnsOf(rows: readonly MatrixRow[]): SeasonColumn[] {
  const width = Math.max(0, ...rows.map((row) => row.codes.length));
  return Array.from({ length: width }, (_, index) => {
    const cells = rows.map((row) => row.codes[index] ?? NO_UNIT).filter((code) => code !== NO_UNIT);
    return {
      share: cells.length > 0 ? countOf(cells, COMPLETE) / cells.length : 0,
      isRunning: cells.includes('r'),
      hasFailed: countOf(cells, 'fw') > 0,
    };
  });
}

function countsOf(rows: readonly MatrixRow[]): MatrixSeason['counts'] {
  const counts = { versions: rows.length, running: 0, failed: 0, complete: 0, pending: 0 };
  for (const row of rows) {
    counts[row.status]++;
  }
  return counts;
}

function isActive(rows: readonly MatrixRow[], options: MatrixRowsOptions): boolean {
  const active = CODES[options.mode]['active'];
  return rows.some((row) => row.version.version === options.next || countOf(row.codes, active) > 0);
}

function seasonOfRows(key: string, rows: MatrixRow[], isOpen: boolean): MatrixSeason {
  const isRanked = key === RANK_GROUP;
  const { major, year } = isRanked ? { major: null, year: null } : seasonOf(key);
  const counts = countsOf(rows);
  return { key, major, year, isRanked, isOpen, rows, counts, columns: columnsOf(rows) };
}

function groupsOf(rows: readonly MatrixRow[]): Map<string, MatrixRow[]> {
  const groups = new Map<string, MatrixRow[]>();
  for (const row of [...rows].sort(newestFirst)) {
    const key = seasonOf(row.version.version).key;
    groups.set(key, [...(groups.get(key) ?? []), row]);
  }
  return groups;
}

/**
 * The rows of the matrix (§ 4.4): the versions grouped by season, newest first, each season
 * with its counts and a summary row per locale. A pass of 40 versions or fewer opens whole;
 * past that, only the seasons where it acts (a unit running, stopped or failed, or the next
 * one; a gap or a failure in a coverage). The operator's choice wins over the default. In
 * the order of the pass, one group sorted by rank, always open.
 */
export function matrixRows(
  versions: readonly MatrixVersion[],
  options: MatrixRowsOptions,
): MatrixSeason[] {
  const rows = versions.map((version) => rowOf(version, options.mode));
  if (options.byRank) {
    const ranked = [...rows].sort(
      (a, b) => (a.version.rank ?? Infinity) - (b.version.rank ?? Infinity),
    );
    return [seasonOfRows(RANK_GROUP, ranked, true)];
  }
  const isSmall = rows.length <= UNFOLDED_VERSIONS;
  return [...groupsOf(rows)]
    .sort(([a], [b]) => Number(b) - Number(a))
    .map(([key, group]) => {
      const isOpen = options.folds.get(key) ?? (isSmall || isActive(group, options));
      return seasonOfRows(key, group, isOpen);
    });
}
