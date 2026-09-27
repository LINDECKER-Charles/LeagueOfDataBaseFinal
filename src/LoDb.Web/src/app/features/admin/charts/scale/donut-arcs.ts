import type { DonutSlice } from './donut-slice';

/** A slice placed on the ring: its dash, the gap to the next and where it starts. */
export interface DonutArc extends DonutSlice {
  readonly dasharray: string;
  readonly dashoffset: number;
  /** Its share of the whole, from 0 to 100. */
  readonly pct: number;
}

const RADIUS = 54;
// Between two arcs, so that neighbours stay apart.
const ARC_GAP = 2;
const PERCENT = 100;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

/**
 * The arcs of a donut drawn with the dashes of one stroked circle of radius 54, no
 * trigonometry: each slice dashes its share of the circumference, starting where the
 * previous one ended. Empty when the whole is nothing.
 */
export function donutArcs(slices: readonly DonutSlice[]): readonly DonutArc[] {
  const total = slices.reduce((sum, slice) => sum + Math.max(0, slice.value), 0);
  if (total <= 0) {
    return [];
  }
  let start = 0;
  return slices.map((slice) => {
    const fraction = Math.max(0, slice.value) / total;
    const dash = Math.max(0, fraction * CIRCUMFERENCE - ARC_GAP);
    const arc: DonutArc = {
      ...slice,
      dasharray: `${dash.toFixed(2)} ${(CIRCUMFERENCE - dash).toFixed(2)}`,
      dashoffset: -start,
      pct: fraction * PERCENT,
    };
    start += fraction * CIRCUMFERENCE;
    return arc;
  });
}
