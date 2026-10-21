import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';

/** A segment of the progress bar, before its label is translated. */
export interface ProgressPart {
  /** written, skipped, failed, left; completed, failed, left when counted in units. */
  readonly key: string;
  readonly value: number;
  /** A CSS colour, a token. */
  readonly color: string;
}

/** How far a pass went, in pages, or in units before any is listed. */
export interface ProgressFigures {
  readonly parts: readonly ProgressPart[];
  /** Pages processed (written, skipped or failed), or units ended. */
  readonly done: number;
  /** The four segments summed: an estimate while units are to come. */
  readonly total: number;
  /** done / total, from 0 to 1. */
  readonly share: number;
  /** Whether the total is estimated: some units have not listed their pages yet. */
  readonly isEstimate: boolean;
  /** Whether it counts units: no unit has listed its pages yet. */
  readonly isUnits: boolean;
}

const GOOD = 'var(--color-good)';
const SKIPPED = 'color-mix(in srgb, var(--color-gold) 70%, transparent)';
const BAD = 'var(--color-bad)';
const TRACK = 'var(--color-track)';

// Before any unit lists its pages, the bar counts units: completed, failed, the rest.
function unitFigures(pass: PrerenderPassView): ProgressFigures {
  const { completed, failed, total } = pass.units;
  const done = completed + failed;
  return {
    parts: [
      { key: 'completed', value: completed, color: GOOD },
      { key: 'failed', value: failed, color: BAD },
      { key: 'left', value: Math.max(0, total - done), color: TRACK },
    ],
    done,
    total,
    share: total > 0 ? done / total : 0,
    isEstimate: false,
    isUnits: true,
  };
}

// What is left drawn so that the bar shows `shown` of it done: the share on screen never
// goes back when the total grows, and the bar follows the figure above it.
function leftShown(done: number, left: number, shown: number): number {
  if (done <= 0 || shown <= done / (done + left)) {
    return left;
  }
  return shown >= 1 ? 0 : Math.max(0, done / shown - done);
}

/**
 * The four segments of the progress of a pass, in pages (§ 4.2): written, skipped, failed,
 * then left, the server's estimate of the pages still to process (`pagesLeft`, by version).
 * The share is processed over their sum; the total is an estimate while units are to come.
 * Before any unit lists its pages, units instead. `shown`, the share on screen, which never
 * goes back for one pass, shortens what is left drawn so that the bar agrees with it.
 */
export function progressSegments(pass: PrerenderPassView, shown = 0): ProgressFigures {
  if (pass.pagesLeft === null || pass.pagesLeft === undefined) {
    return unitFigures(pass);
  }
  const done = pass.pagesWritten + pass.pagesSkipped + pass.pagesFailed;
  const left = Math.max(0, pass.pagesLeft);
  const total = done + left;
  return {
    parts: [
      { key: 'written', value: pass.pagesWritten, color: GOOD },
      { key: 'skipped', value: pass.pagesSkipped, color: SKIPPED },
      { key: 'failed', value: pass.pagesFailed, color: BAD },
      { key: 'left', value: leftShown(done, left, shown), color: TRACK },
    ],
    done,
    total,
    share: total > 0 ? done / total : 0,
    isEstimate: pass.units.pending > 0,
    isUnits: false,
  };
}
