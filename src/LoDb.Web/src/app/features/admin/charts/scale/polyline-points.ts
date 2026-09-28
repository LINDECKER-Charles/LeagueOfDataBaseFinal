/** A point of a chart, in SVG user units. */
export type ChartPoint = readonly [x: number, y: number];

/** The `points` attribute of a polyline or a polygon, to a tenth of a unit. */
export function polylinePoints(points: readonly ChartPoint[]): string {
  return points.map(([x, y]) => `${x.toFixed(1)},${y.toFixed(1)}`).join(' ');
}
