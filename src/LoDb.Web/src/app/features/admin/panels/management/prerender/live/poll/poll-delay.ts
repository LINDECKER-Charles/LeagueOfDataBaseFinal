// The longest a read backs off to: a minute, or five times its cadence when that is longer.
const CEILING_MS = 60_000;
const CEILING_FACTOR = 5;

/**
 * Milliseconds to wait before the next read after `failures` failed ones in a row, from a
 * cadence of `base` (§ 2.5): the cadence itself while reads succeed, then doubling up to its
 * ceiling. No jitter: one tab per operator, and deterministic specs.
 */
export function pollDelay(failures: number, base: number): number {
  if (failures <= 0) {
    return base;
  }
  return Math.min(base * 2 ** failures, Math.max(CEILING_MS, CEILING_FACTOR * base));
}
