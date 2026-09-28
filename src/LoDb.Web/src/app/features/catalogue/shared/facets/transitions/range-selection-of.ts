import type { RangeBounds } from '../../filtering/counts/range-bounds';
import type { RangeSelection } from '../model/range-selection';

/**
 * What a range control commits: its two thumbs put in order, or null when they span the
 * whole of what the cards carry, which filters nothing.
 */
export function rangeSelectionOf(
  low: number,
  high: number,
  bounds: RangeBounds,
): RangeSelection | null {
  const min = Math.min(low, high);
  const max = Math.max(low, high);
  return min <= bounds.min && max >= bounds.max ? null : { min, max };
}
