import type { PrerenderPatchRow } from '../../../../../../../core/api/generated/models/prerender-patch-row';
import { seasonOf } from '../../common/patches/season-of';

/** The pages one unit of a patch is expected to list, and where the figure comes from. */
export interface PagesPerLocale {
  readonly pages: number;
  /**
   * `measured` for the patch itself, `season` the median of its season, `neighbour` the
   * nearest patch measured, `default` without any measure.
   */
  readonly source: 'measured' | 'season' | 'neighbour' | 'default';
  /** The season key (`14`) or the neighbouring version the figure was taken from. */
  readonly from: string | null;
}

// An old patch lists ≈ 300 to 500 pages a locale (496 on average over 402 patches): the
// figure of the spec of the catalogue, used only when nothing was ever measured.
const DEFAULT_PAGES = 500;
// Fewer measures than this make a median no better than the nearest patch.
const SEASON_MEASURES = 3;

function median(values: readonly number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1
    ? sorted[middle]
    : Math.round((sorted[middle - 1] + sorted[middle]) / 2);
}

// The measured patch nearest `index` in the order of the versions; on a tie, the older one,
// which comes later in a list newest first.
function nearest(rows: readonly PrerenderPatchRow[], index: number): PrerenderPatchRow | null {
  let best: PrerenderPatchRow | null = null;
  let bestDistance = Number.POSITIVE_INFINITY;
  for (const [at, row] of rows.entries()) {
    const distance = Math.abs(at - index);
    if (row.pagesPerLocale != null && distance <= bestDistance) {
      best = row;
      bestDistance = distance;
    }
  }
  return best;
}

/**
 * The pages a unit of `version` lists (§ 3.5), from the older patches newest first: its own
 * measure; else the median of its season's, once it has 3 at least; else the nearest patch
 * measured; else 500. Measures come mostly from recent patches, twice to three times larger
 * than old ones: a median over every patch would overestimate a pass on the old ones.
 */
export function pagesPerLocaleOf(
  version: string,
  rows: readonly PrerenderPatchRow[],
): PagesPerLocale {
  const index = rows.findIndex((row) => row.version === version);
  const own = rows[index]?.pagesPerLocale;
  if (own != null) {
    return { pages: own, source: 'measured', from: null };
  }
  const season = seasonOf(version).key;
  const measures = rows
    .filter((row) => seasonOf(row.version).key === season)
    .flatMap((row) => (row.pagesPerLocale == null ? [] : [row.pagesPerLocale]));
  if (measures.length >= SEASON_MEASURES) {
    return { pages: median(measures), source: 'season', from: season };
  }
  const neighbour = nearest(rows, index);
  return neighbour?.pagesPerLocale != null
    ? { pages: neighbour.pagesPerLocale, source: 'neighbour', from: neighbour.version }
    : { pages: DEFAULT_PAGES, source: 'default', from: null };
}
