import type { WarmUpProgress } from '../../api/generated/models/warm-up-progress';
import { isComplete } from './is-complete';

const FULL = 100;

/**
 * The fill of the bar: images fetched over images to fetch, rounded down so that it reads
 * 100% only once the run has landed everything; full at once when there is nothing to
 * fetch. Nothing is counted while the datasets are prepared. A run that ends short, cut by a
 * failure or stopped with images left pending, keeps the share it reached: the bar never
 * falls back.
 */
export function percentOf(frame: WarmUpProgress): number {
  if (isComplete(frame)) {
    return FULL;
  }
  if (frame.stage === 'preparing') {
    return 0;
  }
  if (frame.total === 0) {
    return frame.stage === 'images' ? FULL : 0;
  }
  return Math.min(FULL - 1, Math.floor((frame.settled / frame.total) * FULL));
}
