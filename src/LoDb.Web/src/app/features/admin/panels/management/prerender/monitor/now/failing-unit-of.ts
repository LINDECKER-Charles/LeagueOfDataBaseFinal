import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderUnitView } from '../../../../../../../core/api/generated/models/prerender-unit-view';

/** A running unit whose pages fail in numbers. */
export interface FailingUnit {
  readonly unit: PrerenderUnitView;
  /** Pages processed so far: written, skipped or failed. */
  readonly processed: number;
  /** Failed over processed, 0 to 1. */
  readonly share: number;
}

const RUNNING = 'running';
// Past a fifth of its pages failed, a unit is failing; under 10 pages, nothing is told yet.
const FAILING_SHARE = 0.2;
const MIN_PAGES = 10;

/**
 * The unit of a running pass that fails in numbers (§ 4.5): a fifth of its pages processed
 * or more failed, over 10 pages at least. The failures list waits for a unit to end and the
 * stall counts a failure as activity: without it, a store refusing every page would go
 * unseen until the unit ends. Null for a paused pass, or while every unit holds.
 */
export function failingUnitOf(progress: PrerenderPassProgress): FailingUnit | null {
  if (progress.pass.state !== RUNNING) {
    return null;
  }
  for (const unit of progress.running) {
    const processed = unit.pagesWritten + unit.pagesSkipped + unit.pagesFailed;
    const share = processed > 0 ? unit.pagesFailed / processed : 0;
    if (processed >= MIN_PAGES && share >= FAILING_SHARE) {
      return { unit, processed, share };
    }
  }
  return null;
}
