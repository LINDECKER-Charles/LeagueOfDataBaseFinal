import type { PrerenderPassProgress } from '../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderVersionProgress } from '../../../../../../core/api/generated/models/prerender-version-progress';
import { type ActivityEvent, activityEvents } from './activity/activity-events';
import { type WrittenAt, writtenAtOf } from './now/written-at';
import { type Eta, etaOf } from './progress/eta-of';
import { PaceSamples } from './progress/pace-samples';
import { progressSegments } from './progress/progress-segments';

/** What the monitor holds of a pass, answer after answer: the value of its live read. */
export interface MonitorState {
  /** The last answer, its versions every one seen so far, its scope list kept. */
  readonly progress: PrerenderPassProgress;
  /** The pace of the pass as the monitor saw it. */
  readonly samples: PaceSamples;
  /** The activity feed, newest first, 50 at most. */
  readonly events: readonly ActivityEvent[];
  /** The units of the feed seen so far. */
  readonly seen: ReadonlySet<string>;
  /** The share on screen, from 0 to 1: it never goes back for one pass. */
  readonly shownShare: number;
  /** The end, at the mean pace and the current one, with its hysteresis. */
  readonly eta: Eta;
  /** When the pass last wrote a page: what a stall is measured from. */
  readonly written: WrittenAt;
  /** Date.now() when the answer came: with generatedAt, the server's clock. */
  readonly receivedAt: number;
  /** Date.now() when the last complete answer came, to read whole again past 10 minutes. */
  readonly completeAt: number;
}

const MAX_EVENTS = 50;

/** What the monitor carries from one answer of a pass to the next. */
interface Seed {
  readonly last: MonitorState | undefined;
  readonly samples: PaceSamples;
  readonly events: readonly ActivityEvent[];
  readonly seen: ReadonlySet<string>;
  readonly shownShare: number;
  readonly isCurrentShown: boolean;
  readonly completeAt: number;
  readonly written: WrittenAt | undefined;
}

// Every version seen: a complete answer replaces them, an incremental one updates or adds
// those that changed. None ever goes: a unit is never deleted. In the worker's order.
function versionsOf(
  last: MonitorState | undefined,
  response: PrerenderPassProgress,
): PrerenderVersionProgress[] {
  if (response.isComplete || !last) {
    return [...response.versions];
  }
  const merged = new Map(last.progress.versions.map((version) => [version.version, version]));
  for (const version of response.versions) {
    merged.set(version.version, version);
  }
  return [...merged.values()].sort((one, other) => one.rank - other.rank);
}

function progressOf(
  last: MonitorState | undefined,
  response: PrerenderPassProgress,
): PrerenderPassProgress {
  const scopeVersions = response.isComplete
    ? (response.scopeVersions ?? null)
    : (last?.progress.scopeVersions ?? null);
  return { ...response, versions: versionsOf(last, response), scopeVersions };
}

// What the monitor holds after `response`, whatever it held before (`seed`).
function stateOf(seed: Seed, response: PrerenderPassProgress, receivedAt: number): MonitorState {
  const generatedAt = Date.parse(response.generatedAt);
  const samples = seed.samples.add(response.pass, generatedAt);
  const activity = activityEvents(seed.last, response, seed.seen);
  const eta = etaOf(
    {
      pass: response.pass,
      recentPace: samples.recent(),
      instantPace: samples.instant(),
      serverNow: generatedAt,
    },
    seed.isCurrentShown,
  );
  return {
    progress: progressOf(seed.last, response),
    samples,
    events: [...activity.events, ...seed.events].slice(0, MAX_EVENTS),
    seen: activity.seen,
    shownShare: Math.max(seed.shownShare, progressSegments(response.pass).share),
    eta,
    written: writtenAtOf(seed.written, response),
    receivedAt,
    completeAt: response.isComplete ? receivedAt : seed.completeAt,
  };
}

function first(response: PrerenderPassProgress, receivedAt: number): MonitorState {
  const seed: Seed = {
    last: undefined,
    samples: PaceSamples.empty(),
    events: [],
    seen: new Set(),
    shownShare: 0,
    isCurrentShown: false,
    completeAt: 0,
    written: undefined,
  };
  return stateOf(seed, response, receivedAt);
}

function next(last: MonitorState, response: PrerenderPassProgress, receivedAt: number) {
  const seed: Seed = { ...last, last, isCurrentShown: last.eta.isCurrentShown };
  return stateOf(seed, response, receivedAt);
}

/**
 * Folds an answer of the progress into what the monitor holds (§ 4.9): the versions merged
 * (a complete answer replaces them), the scope list kept from the last complete answer, a
 * sample of the pace, the new lines of the feed, the share on screen (never back for one
 * pass: it waits for the figure to catch up when the total grows) and the end with its
 * hysteresis, and when it last wrote a page. Another pass than the last starts over.
 * `receivedAt` is Date.now() at the answer.
 */
export function mergeProgress(
  last: MonitorState | undefined,
  response: PrerenderPassProgress,
  receivedAt: number,
): MonitorState {
  const same = last?.progress.pass.id === response.pass.id ? last : undefined;
  return same ? next(same, response, receivedAt) : first(response, receivedAt);
}
