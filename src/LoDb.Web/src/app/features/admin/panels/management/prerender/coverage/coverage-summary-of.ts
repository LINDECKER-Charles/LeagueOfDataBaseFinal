import type { PrerenderPatchesReport } from '../../../../../../core/api/generated/models/prerender-patches-report';
import { type PatchStatus, patchStatusOf } from '../common/patches/patch-status-of';
import { seasonOf } from '../common/patches/season-of';

/** What a season holds, for its small bar: clean, partial or with failed pages, failed, absent. */
export interface SeasonShares {
  readonly key: string;
  readonly major: number;
  readonly year: number | null;
  readonly complete: number;
  /** Partial, and complete with failed pages: neither whole nor missing. */
  readonly partial: number;
  readonly failed: number;
  readonly absent: number;
}

/** The summary above the coverage table (§ 5.2). */
export interface CoverageSummary {
  /** Patches complete in every locale, failed pages or not. */
  readonly complete: number;
  readonly partial: number;
  readonly absent: number;
  readonly failed: number;
  readonly total: number;
  /** Units complete over the units expected, from 0 to 1; null when none is expected. */
  readonly share: number | null;
  /** The patches with a gap, absent or partial: what « Compléter les manques » renders. */
  readonly gapVersions: readonly string[];
  readonly seasons: readonly SeasonShares[];
}

type Share = 'complete' | 'partial' | 'failed' | 'absent';
type Shares = { -readonly [K in keyof SeasonShares]: SeasonShares[K] };

const GAPS = /[-p]/;

function seasonsOf(rows: readonly { version: string; status: PatchStatus }[]): SeasonShares[] {
  const seasons = new Map<string, Shares>();
  for (const { version, status } of rows) {
    const { key, major, year } = seasonOf(version);
    const season = seasons.get(key) ?? {
      key,
      major,
      year,
      complete: 0,
      partial: 0,
      failed: 0,
      absent: 0,
    };
    const share: Share = status === 'complete_with_failures' ? 'partial' : status;
    season[share] += 1;
    seasons.set(key, season);
  }
  return [...seasons.values()];
}

/**
 * The summary of what an epoch holds of the older patches: the patches by status
 * (`patchStatusOf`), the share of the units complete (the API's count, over every older
 * patch), the patches with a gap, and season by season what its small bar draws.
 */
export function coverageSummaryOf(report: PrerenderPatchesReport): CoverageSummary {
  const rows = report.patches.map((row) => ({ version: row.version, status: patchStatusOf(row) }));
  const count = (...statuses: PatchStatus[]) =>
    rows.filter((row) => statuses.includes(row.status)).length;
  const { completeUnits, expectedUnits } = report.coverage;
  return {
    complete: count('complete', 'complete_with_failures'),
    partial: count('partial'),
    absent: count('absent'),
    failed: count('failed'),
    total: rows.length,
    share: expectedUnits > 0 ? completeUnits / expectedUnits : null,
    gapVersions: report.patches.filter((row) => GAPS.test(row.cells)).map((row) => row.version),
    seasons: seasonsOf(rows),
  };
}
