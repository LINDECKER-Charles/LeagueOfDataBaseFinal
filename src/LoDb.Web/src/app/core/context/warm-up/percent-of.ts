import type { WarmUpProgress } from '../../api/generated/models/warm-up-progress';

const FULL = 100;

/**
 * The fill of the bar: images fetched over images to fetch, rounded down so that it reads
 * 100% only once the run is done; full at once when there is nothing to fetch. Nothing is
 * counted while the datasets are prepared, or after a failure.
 */
export function percentOf(frame: WarmUpProgress): number {
  if (frame.stage === 'preparing' || frame.stage === 'failed') {
    return 0;
  }
  if (frame.stage === 'done' || frame.total === 0) {
    return FULL;
  }
  return Math.min(FULL - 1, Math.floor((frame.settled / frame.total) * FULL));
}
