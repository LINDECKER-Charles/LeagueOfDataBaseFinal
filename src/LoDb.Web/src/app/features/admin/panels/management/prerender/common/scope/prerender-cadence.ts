import { InjectionToken } from '@angular/core';

/** How often the pre-rendering page reads each of its sources, in milliseconds (§ 2.5). */
export interface PrerenderCadence {
  /** The progress of a running pass: its units move every few seconds. */
  readonly progressRunningMs: number;
  /** The progress of a paused pass: nothing moves but a resume or a promotion. */
  readonly progressPausedMs: number;
  /** The report while a pass is active. */
  readonly reportActiveMs: number;
  /** The report without one: a promotion may open a pass meanwhile. */
  readonly reportIdleMs: number;
  /** The report while the tab is hidden, so its title and the end notice stay true. */
  readonly reportHiddenMs: number;
  /** The units of a version, while one of them runs. */
  readonly versionMs: number;
}

const CADENCE: PrerenderCadence = {
  progressRunningMs: 5_000,
  progressPausedMs: 30_000,
  reportActiveMs: 60_000,
  reportIdleMs: 120_000,
  reportHiddenMs: 300_000,
  versionMs: 10_000,
};

/** The cadence of the pre-rendering page; a token so that a spec can read at once. */
export const PRERENDER_CADENCE = new InjectionToken<PrerenderCadence>('PRERENDER_CADENCE', {
  providedIn: 'root',
  factory: () => CADENCE,
});
