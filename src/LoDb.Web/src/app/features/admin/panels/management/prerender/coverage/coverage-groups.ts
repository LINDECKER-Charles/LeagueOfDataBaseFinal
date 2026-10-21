import type { PrerenderPatchRow } from '../../../../../../core/api/generated/models/prerender-patch-row';
import { type PatchStatus, patchStatusOf } from '../common/patches/patch-status-of';
import { seasonOf } from '../common/patches/season-of';
import { patchMatches } from '../common/selection/patch-matches';

/** The statuses the table filters on; « complets » takes those with failed pages too. */
export type StatusFilter = 'all' | 'complete' | 'partial' | 'absent' | 'failed';

/** What the coverage table shows of the older patches. */
export interface CoverageFilter {
  /** The text filter, as the launch dialog reads it (`patchMatches`). */
  readonly query: string;
  readonly status: StatusFilter;
}

/** A season of the coverage table: its patches that pass the filter. */
export interface CoverageGroup {
  /** The season's first segment, `16`. */
  readonly key: string;
  readonly major: number;
  readonly year: number | null;
  readonly rows: readonly PrerenderPatchRow[];
  /** Its rows complete in every locale, with or without failed pages. */
  readonly complete: number;
  /** Its rows with a gap, a cell absent or partial: what « Compléter » renders. */
  readonly missing: number;
}

const GAPS = /[-p]/;
const FILTERED: Readonly<Record<StatusFilter, readonly PatchStatus[] | null>> = {
  all: null,
  complete: ['complete', 'complete_with_failures'],
  partial: ['partial'],
  absent: ['absent'],
  failed: ['failed'],
};

// Whether a patch has a gap in the epoch: a locale absent (-) or partial (p).
function hasGap(row: PrerenderPatchRow): boolean {
  return GAPS.test(row.cells);
}

function passes(row: PrerenderPatchRow, filter: CoverageFilter): boolean {
  const statuses = FILTERED[filter.status];
  const isKept = statuses === null || statuses.includes(patchStatusOf(row));
  return isKept && patchMatches(row.version, filter.query);
}

function groupOf(key: string, rows: readonly PrerenderPatchRow[]): CoverageGroup {
  const { major, year } = seasonOf(key);
  return {
    key,
    major,
    year,
    rows,
    complete: rows.filter((row) => FILTERED.complete?.includes(patchStatusOf(row))).length,
    missing: rows.filter(hasGap).length,
  };
}

/**
 * The older patches of the coverage table (§ 5.2), by season in the order of the report
 * (newest first), each season holding its patches that pass the text and status filters; a
 * season left empty by them is dropped.
 */
export function coverageGroups(
  patches: readonly PrerenderPatchRow[],
  filter: CoverageFilter,
): CoverageGroup[] {
  const seasons = new Map<string, PrerenderPatchRow[]>();
  for (const row of patches.filter((patch) => passes(patch, filter))) {
    const { key } = seasonOf(row.version);
    seasons.set(key, [...(seasons.get(key) ?? []), row]);
  }
  return [...seasons].map(([key, rows]) => groupOf(key, rows));
}
