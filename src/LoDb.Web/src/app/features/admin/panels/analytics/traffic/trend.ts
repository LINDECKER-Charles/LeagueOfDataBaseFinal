/**
 * How a figure moved since the period before: up or down by a share, about level, or with
 * nothing to compare (all the history, or a period before that saw nothing).
 */
export interface Trend {
  readonly direction: 'up' | 'down' | 'flat' | 'none';
  /** The change, from 0 to 1 and beyond, without its sign. */
  readonly change: number;
}

// Under half a percent either way, a figure is held level.
const LEVEL = 0.005;

export function trendOf(current: number, previous: number | null | undefined): Trend {
  if (previous === null || previous === undefined || previous <= 0) {
    return { direction: 'none', change: 0 };
  }
  const change = (current - previous) / previous;
  if (Math.abs(change) < LEVEL) {
    return { direction: 'flat', change: 0 };
  }
  return { direction: change > 0 ? 'up' : 'down', change: Math.abs(change) };
}
