import { type ChartPoint, polylinePoints } from './polyline-points';

/** A stretch of a sparkline drawn in one go: its line and the area under it. */
export interface SparklineRun {
  readonly line: string;
  readonly area: string;
}

/** What a sparkline draws: its box, then each run of its line between the gaps. */
export interface SparklineShape {
  readonly viewBox: string;
  readonly runs: readonly SparklineRun[];
}

const W = 120;
const H = 30;
// Room above and below, so that the extreme points are not clipped by the box.
const PAD = 2;
const MIN_RUN = 2;

function runOf(points: readonly ChartPoint[]): SparklineRun {
  const first = points[0]?.[0] ?? 0;
  const last = points.at(-1)?.[0] ?? W;
  return {
    line: polylinePoints(points),
    area: polylinePoints([[first, H], ...points, [last, H]]),
  };
}

// The points split where the line breaks: a run starts at each gap.
function runsOf(points: readonly ChartPoint[], gaps: readonly number[]): ChartPoint[][] {
  const runs: ChartPoint[][] = [[]];
  points.forEach((point, index) => {
    if (index > 0 && gaps.includes(index)) {
      runs.push([]);
    }
    runs.at(-1)?.push(point);
  });
  return runs.filter((run) => run.length >= MIN_RUN);
}

/**
 * The trend of a tile, without axes: the whole width, the lowest value at the bottom, the
 * highest at the top, a flat series along the bottom. `gaps` are the indices where the line
 * breaks, nothing being known between a value and the one before (a reading missed): the
 * runs between them are drawn apart, and a run of a single value draws nothing. Null under
 * two values: a trend needs two points.
 */
export function sparklineShape(
  values: readonly number[],
  gaps: readonly number[] = [],
): SparklineShape | null {
  if (values.length < 2) {
    return null;
  }
  const min = Math.min(...values);
  const span = Math.max(...values) - min || 1;
  const points: ChartPoint[] = values.map((value, index) => [
    (index * W) / (values.length - 1),
    H - PAD - ((value - min) / span) * (H - 2 * PAD),
  ]);
  return { viewBox: `0 0 ${W} ${H}`, runs: runsOf(points, gaps).map(runOf) };
}
