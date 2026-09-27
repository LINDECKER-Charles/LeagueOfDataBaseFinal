// A single hit stays visible: the lowest share a non-empty cell gets.
const FLOOR = 10;
const SPAN = 90;
// Below 1, the ramp lifts its low end, so that it reads perceptually even.
const GAMMA = 0.6;
const PRECISION = 10;

/**
 * The fill of a heatmap cell: one hue, the Hextech cyan, more opaque as the cell nears the
 * busiest one; an empty cell shows the track. A token mixed at paint time, where the legacy
 * admin wrote the channels of the cyan by hand.
 */
export function heatColor(value: number, max: number): string {
  if (max <= 0 || value <= 0) {
    return 'color-mix(in srgb, var(--color-gold-deep) 14%, transparent)';
  }
  const eased = Math.min(1, value / max) ** GAMMA;
  const share = Math.round((FLOOR + SPAN * eased) * PRECISION) / PRECISION;
  return `color-mix(in srgb, var(--color-hex) ${share}%, transparent)`;
}
