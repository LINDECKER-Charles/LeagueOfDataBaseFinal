import type { BarSegment } from './stacked-bar';

/** A segment of a stacked bar with its share of the bar, from 0 to 1. */
export interface StackedSegment extends BarSegment {
  readonly share: number;
}

// Two pixels of a 500 pixel bar: a handful of failures on two million pages still shows.
const MIN_SHARE = 0.004;

/**
 * The shares of a stacked bar, summing to 1 (§ 4.2): each segment its value over the total,
 * a segment of nothing left at 0, and a tiny segment that is not nothing raised to `minimum`,
 * the others giving up its room in proportion. A total of nothing gives nothing to draw.
 */
export function stackedSegments(
  segments: readonly BarSegment[],
  minimum = MIN_SHARE,
): StackedSegment[] {
  const total = segments.reduce((sum, segment) => sum + Math.max(0, segment.value), 0);
  if (total <= 0) {
    return segments.map((segment) => ({ ...segment, share: 0 }));
  }
  const raw = segments.map((segment) => Math.max(0, segment.value) / total);
  const isTiny = (share: number) => share > 0 && share < minimum;
  const raised = raw.filter(isTiny).length * minimum;
  const rest = raw.filter((share) => !isTiny(share)).reduce((sum, share) => sum + share, 0);
  if (raised >= 1 || rest <= 0) {
    return segments.map((segment, index) => ({ ...segment, share: raw[index] }));
  }
  const scale = (1 - raised) / rest;
  return segments.map((segment, index) => ({
    ...segment,
    share: isTiny(raw[index]) ? minimum : raw[index] * scale,
  }));
}
