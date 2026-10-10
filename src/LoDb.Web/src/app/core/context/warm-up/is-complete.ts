import type { WarmUpProgress } from '../../api/generated/models/warm-up-progress';

/**
 * Whether a run landed everything it set out to fetch. A run the API stopped early, its
 * image work refused while the site is busy, is done as well, with images left pending that
 * the page fills as they arrive: it is not complete.
 */
export function isComplete(frame: WarmUpProgress): boolean {
  return frame.stage === 'done' && frame.settled >= frame.total;
}
