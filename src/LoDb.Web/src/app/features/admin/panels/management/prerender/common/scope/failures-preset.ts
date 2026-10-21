import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderVersionProgress } from '../../../../../../../core/api/generated/models/prerender-version-progress';
import type { LaunchPreset } from './launch-preset';

/** The relaunch of what a pass left undone, and what it renders again. */
export interface FailuresPreset {
  readonly preset: LaunchPreset;
  /** Cells kept: what the relaunch renders again, `failed` and `gaps` together. */
  readonly units: number;
  /** Of them, the units failed or with failed pages (f, w). */
  readonly failed: number;
  /** Of them, the units a cancelled pass never rendered whole (., i, z). */
  readonly gaps: number;
  /** ≈ pages of those units. */
  readonly pages: number;
}

// The cells of a pass (PrerenderVersionProgress.cells): f failed, w completed with failed
// pages; in a cancelled pass also . never reached, i and z cut short.
const FAILED = 'fw';
const GAPS = '.iz';
// The cells whose unit listed its pages: what a version's pages are measured over.
const LISTED = 'rzcwfi';
const CANCELLED = 'cancelled';

function count(cells: string, codes: string): number {
  return [...cells].filter((code) => codes.includes(code)).length;
}

// The pages of one unit of `version`: its own mean, else the pass's, else nothing known.
function pagesPerUnit(version: PrerenderVersionProgress, passMean: number): number {
  const listed = count(version.cells, LISTED);
  return listed > 0 ? version.pagesTotal / listed : passMean;
}

function passMeanOf(versions: readonly PrerenderVersionProgress[]): number {
  const listed = versions.reduce((sum, version) => sum + count(version.cells, LISTED), 0);
  const pages = versions.reduce((sum, version) => sum + version.pagesTotal, 0);
  return listed > 0 ? pages / listed : 0;
}

// The pass's epoch, unless retired since (older than the served one): then the served one,
// as relaunchPreset does; the API refuses to write a retired epoch.
function presetOf(
  pass: PrerenderPassView,
  versions: readonly string[],
  servedEpoch: number,
): LaunchPreset {
  return {
    origin: 'failures',
    versions,
    priority: pass.scope.priority.filter((version) => versions.includes(version)),
    locales: pass.scope.locales ?? null,
    pageTypes: pass.scope.pageTypes ?? null,
    epoch: pass.epoch < servedEpoch ? servedEpoch : pass.epoch,
    skipComplete: true,
    fromPass: pass.id,
  };
}

/**
 * The relaunch of the versions a pass did not complete cleanly (§ 3.1): those with a cell
 * failed or with failed pages, and for a cancelled pass those it never reached or cut short;
 * over the pass's locales, types and epoch (the served one when it was retired since), leaving
 * out what the epoch already holds. Its
 * units and pages say on the button what it renders again: a unit with two failed pages is
 * rendered whole. Null when nothing is to render again.
 */
export function failuresPreset(
  pass: PrerenderPassView,
  versions: readonly PrerenderVersionProgress[],
  servedEpoch: number,
): FailuresPreset | null {
  const codes = pass.state === CANCELLED ? FAILED + GAPS : FAILED;
  const passMean = passMeanOf(versions);
  const kept = versions.filter((version) => count(version.cells, codes) > 0);
  if (kept.length === 0) {
    return null;
  }
  const units = kept.reduce((sum, version) => sum + count(version.cells, codes), 0);
  const failed = kept.reduce((sum, version) => sum + count(version.cells, FAILED), 0);
  const pages = kept.reduce(
    (sum, version) => sum + count(version.cells, codes) * pagesPerUnit(version, passMean),
    0,
  );
  return {
    preset: presetOf(
      pass,
      kept.map((version) => version.version),
      servedEpoch,
    ),
    units,
    failed,
    gaps: units - failed,
    pages: Math.round(pages),
  };
}
