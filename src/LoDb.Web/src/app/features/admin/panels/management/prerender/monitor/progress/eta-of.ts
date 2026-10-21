import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';

/** What the end of a pass is estimated from. */
export interface EtaInput {
  readonly pass: PrerenderPassView;
  /** Pages a minute over the last 15 minutes (PaceSamples.recent), null while unknown. */
  readonly recentPace: number | null;
  /**
   * Pages a minute over the last 2 minutes (PaceSamples.instant), null while unknown: the pace
   * the monitor shows, the end stands on it while neither the mean nor the recent one is known.
   */
  readonly instantPace: number | null;
  /** The server's clock, in milliseconds. */
  readonly serverNow: number;
}

/** When a pass should end, at its mean pace and at the current one. */
export interface Eta {
  /** known: an end is estimated; unknown: no pace yet; paused: on hold while paused. */
  readonly state: 'known' | 'unknown' | 'paused';
  /** At the mean pace, the server's own estimate, in milliseconds. */
  readonly average: number | null;
  /** At the recent pace, in milliseconds. */
  readonly current: number | null;
  /** Whether « au rythme actuel » shows, under its hysteresis. */
  readonly isCurrentShown: boolean;
}

const PAUSED = 'paused';
const MINUTE_MS = 60_000;
// The second line comes when the two ends differ by more than 15 %, goes under 5 %, and
// keeps its state between the two: a pace wavering around a threshold does not blink it.
const SHOW_OVER = 0.15;
const HIDE_UNDER = 0.05;

function endAt(input: EtaInput, pace: number | null): number | null {
  const left = input.pass.pagesLeft;
  if (pace === null || pace <= 0 || left === null || left === undefined) {
    return null;
  }
  return input.serverNow + (Math.max(0, left) / pace) * MINUTE_MS;
}

/** The two ends of a pass, and the server's clock they are measured from. */
interface Ends {
  readonly average: number;
  readonly current: number;
  readonly serverNow: number;
}

function isShown({ average, current, serverNow }: Ends, was: boolean): boolean {
  const remaining = average - serverNow;
  if (remaining <= 0) {
    return false;
  }
  const gap = Math.abs(current - average) / remaining;
  if (gap > SHOW_OVER) {
    return true;
  }
  return gap < HIDE_UNDER ? false : was;
}

/**
 * The end of a pass (§ 4.3): at its mean pace, the server's `estimatedEndAt`; at the current
 * pace, the pages left over the pace of the last 15 minutes, which a change of version or a
 * wait does not drop as the 2-minute one does; while neither is known (a page just opened on a
 * pass just resumed), over the 2-minute pace the monitor shows. The current one shows beside the mean one when
 * they differ, with a hysteresis: `wasCurrentShown` is its state at the previous answer.
 */
export function etaOf(input: EtaInput, wasCurrentShown: boolean): Eta {
  if (input.pass.state === PAUSED) {
    return { state: 'paused', average: null, current: null, isCurrentShown: false };
  }
  const end = input.pass.estimatedEndAt;
  const average = end ? Date.parse(end) : null;
  // Never « inconnue » beside a pace on screen: the instant one stands in for the others.
  const current =
    endAt(input, input.recentPace) ?? (average === null ? endAt(input, input.instantPace) : null);
  const isCurrentShown =
    average !== null &&
    current !== null &&
    isShown({ average, current, serverNow: input.serverNow }, wasCurrentShown);
  const state = average === null && current === null ? 'unknown' : 'known';
  return { state, average, current, isCurrentShown };
}
