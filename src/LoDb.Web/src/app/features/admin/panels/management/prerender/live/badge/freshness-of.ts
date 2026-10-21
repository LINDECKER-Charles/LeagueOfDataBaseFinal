/**
 * How fresh what a live read shows is (§ 2.5): `live` within two cadences, `late` within a
 * minute or four cadences, `lost` past that, `hidden` while the tab is hidden.
 */
export type Freshness = 'live' | 'late' | 'lost' | 'hidden';

/** What freshness is judged on, all in the client's clock: no skew with the server's. */
export interface FreshnessInput {
  /** Date.now() at the last success, null before any. */
  readonly lastSuccessAt: number | null;
  /** The cadence of the reads, in milliseconds. */
  readonly every: number;
  readonly isHidden: boolean;
}

// The time an answer takes to come back, on top of the cadences.
const ANSWER_MS = 2_000;
const LIVE_CADENCES = 2;
const LATE_CADENCES = 4;
const LATE_FLOOR_MS = 60_000;

/** The freshness at `now`; null before the first answer, which nothing dates yet. */
export function freshnessOf(input: FreshnessInput, now: number): Freshness | null {
  if (input.isHidden) {
    return 'hidden';
  }
  if (input.lastSuccessAt === null) {
    return null;
  }
  const age = now - input.lastSuccessAt;
  if (age <= LIVE_CADENCES * input.every + ANSWER_MS) {
    return 'live';
  }
  return age <= Math.max(LATE_FLOOR_MS, LATE_CADENCES * input.every) ? 'late' : 'lost';
}
