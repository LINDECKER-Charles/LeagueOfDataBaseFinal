import { HttpClient } from '@angular/common/http';
import { effect, inject, untracked } from '@angular/core';
import { map } from 'rxjs';
import { API_BASE_URL } from '../../../../../../core/api/api-base-url';
import { readAdminPrerenderPassProgress } from '../../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender-pass-progress';
import { PRERENDER_CADENCE, type PrerenderCadence } from '../common/scope/prerender-cadence';
import { type LivePoll, injectLivePoll } from '../live/poll/inject-live-poll';
import { type MonitorState, mergeProgress } from './merge-progress';

const RUNNING = 'running';
const PAUSED = 'paused';
// The answers overlap by 30 seconds: a transaction that committed after the last read, but
// stamped before it, is still read.
const OVERLAP_MS = 30_000;
// Past 10 minutes without a whole read, or since the last answer, the next reads it whole.
const RESYNC_MS = 600_000;

// The `since` of the next read: none for a first read, another pass, or a resync.
function sinceOf(last: MonitorState | undefined, passId: number, now: number): string | undefined {
  if (!last || last.progress.pass.id !== passId) {
    return undefined;
  }
  if (now - last.completeAt > RESYNC_MS || now - last.receivedAt > RESYNC_MS) {
    return undefined;
  }
  return new Date(Date.parse(last.progress.generatedAt) - OVERLAP_MS).toISOString();
}

// Every 5 seconds while it runs, every 30 while paused, no more once it ended.
function cadenceOf(cadence: PrerenderCadence, state: string): number | null {
  if (state === RUNNING) {
    return cadence.progressRunningMs;
  }
  return state === PAUSED ? cadence.progressPausedMs : null;
}

// Another pass than the one followed reads at once, without waiting for the cadence.
function followPass(passId: () => number, poll: LivePoll<MonitorState>): void {
  let followed: number | null = null;
  effect(() => {
    const id = passId();
    if (followed !== null && followed !== id) {
      untracked(() => poll.reload());
    }
    followed = id;
  });
}

/**
 * Reads the progress of a pass over and over (§ 4.9), from the injection context of a
 * component: every 5 seconds while it runs, every 30 while paused, no more once it ended;
 * suspended while the tab is hidden. Each read asks only what changed since the last answer
 * (less 30 seconds), and reads whole at first, for another pass, and past 10 minutes; the
 * answer is folded into what the monitor holds (`mergeProgress`). `state` is the pass's state
 * as last known; another `passId` reads again at once.
 */
export function injectPassProgress(
  passId: () => number,
  state: () => string,
): LivePoll<MonitorState> {
  const http = inject(HttpClient);
  const rootUrl = inject(API_BASE_URL);
  const cadence = inject(PRERENDER_CADENCE);
  const poll = injectLivePoll<MonitorState>({
    load: (last) => {
      const id = passId();
      const since = sinceOf(last, id, Date.now());
      return readAdminPrerenderPassProgress(http, rootUrl, { id, since }).pipe(
        map((response) => mergeProgress(last, response.body, Date.now())),
      );
    },
    every: () => cadenceOf(cadence, state()),
  });
  followPass(passId, poll);
  return poll;
}
