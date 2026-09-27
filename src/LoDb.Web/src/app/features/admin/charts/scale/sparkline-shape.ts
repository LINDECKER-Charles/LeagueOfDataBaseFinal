import { type ChartPoint, polylinePoints } from './polyline-points';

/** What a sparkline draws: its box, its line and the area under it. */
export interface SparklineShape {
  readonly viewBox: string;
  readonly line: string;
  readonly area: string;
}

const W = 120;
const H = 30;
// Room above and below, so that the extreme points are not clipped by the box.
const PAD = 2;

/**
 * The trend of a tile, without axes: the whole width, the lowest value at the bottom, the
 * highest at the top, a flat series along the bottom. Null under two values: a trend needs
 * two points.
 */
export function sparklineShape(values: readonly number[]): SparklineShape | null {
  if (values.length < 2) {
    return null;
  }
  const min = Math.min(...values);
  const span = Math.max(...values) - min || 1;
  const points: ChartPoint[] = values.map((value, index) => [
    (index * W) / (values.length - 1),
    H - PAD - ((value - min) / span) * (H - 2 * PAD),
  ]);
  return {
    viewBox: `0 0 ${W} ${H}`,
    line: polylinePoints(points),
    area: polylinePoints([[0, H], ...points, [W, H]]),
  };
}
