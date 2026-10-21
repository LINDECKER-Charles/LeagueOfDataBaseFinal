import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';
import type { PrerenderPassView } from '../../../../../../../core/api/generated/models/prerender-pass-view';
import type { PrerenderUnitView } from '../../../../../../../core/api/generated/models/prerender-unit-view';

/** What happened, each a `live.feed.*` key. */
export type ActivityKind =
  | 'done'
  | 'done_failures'
  | 'failed'
  | 'joined'
  | 'total_up'
  | 'total_up_joined'
  | 'paused'
  | 'paused_itself'
  | 'resumed'
  | 'completed'
  | 'cancelled';

/** A line of the activity feed (§ 4.6), before its text is translated. */
export interface ActivityEvent {
  /** Unique within a pass: what the feed tracks. */
  readonly key: string;
  readonly kind: ActivityKind;
  /** When it happened, on the server's clock (ISO). */
  readonly at: string;
  readonly version?: string;
  readonly locale?: string;
  /** Pages of a unit, or pages a total grew by. */
  readonly pages?: number;
  readonly failed?: number;
  /** Units of a version that joined the pass. */
  readonly units?: number;
  /** How long a unit took, in seconds. */
  readonly seconds?: number;
  /** Why a unit failed or the pass paused itself, in the English the pass writes. */
  readonly reason?: string | null;
}

/** What the monitor knew before an answer: the last one, its versions every one seen. */
export interface ActivityBefore {
  readonly progress: PrerenderPassProgress;
}

const SECOND_MS = 1_000;
// A total re-estimated by more than 1 % says so: it is why the share on screen marks time.
const TOTAL_STEP = 1.01;
const NO_UNIT = '_';
const STATE_EVENTS: Readonly<Record<string, ActivityKind>> = {
  running: 'resumed',
  completed: 'completed',
  cancelled: 'cancelled',
};

/** The new lines of the feed, and the units seen so far. */
export interface ActivityUpdate {
  /** Newest first. */
  readonly events: readonly ActivityEvent[];
  /** The units of `recent` seen, to pass to the next call. */
  readonly seen: ReadonlySet<string>;
}

// A unit as it ended: a unit seen once never comes back to the feed, and one leaves
// `recent` only for newer ones, so the keys of the last `recent` are all to remember.
function unitSeenKey(unit: PrerenderUnitView): string {
  return `${unit.version}|${unit.locale}|${unit.updatedAt}`;
}

function unitKind(unit: PrerenderUnitView): ActivityKind {
  if (unit.state === 'failed') {
    return 'failed';
  }
  return unit.pagesFailed > 0 ? 'done_failures' : 'done';
}

function unitEvent(unit: PrerenderUnitView): ActivityEvent {
  const ended = unit.endedAt ?? unit.updatedAt;
  const started = unit.startedAt ? Date.parse(unit.startedAt) : Date.parse(ended);
  const kind = unitKind(unit);
  return {
    key: `unit:${unitSeenKey(unit)}`,
    kind,
    at: ended,
    version: unit.version,
    locale: unit.locale,
    pages: unit.pagesTotal,
    failed: unit.pagesFailed,
    seconds: Math.max(0, Math.round((Date.parse(ended) - started) / SECOND_MS)),
    reason: kind === 'done' ? null : (unit.failure ?? null),
  };
}

function stateKind(next: PrerenderPassView): ActivityKind | undefined {
  if (next.state !== 'paused') {
    return STATE_EVENTS[next.state];
  }
  return next.pauseReason ? 'paused_itself' : 'paused';
}

function stateEvent(
  before: PrerenderPassView,
  next: PrerenderPassView,
  at: string,
): ActivityEvent[] {
  const kind = before.state === next.state ? undefined : stateKind(next);
  const reason = kind === 'paused_itself' ? next.pauseReason : null;
  return kind ? [{ key: `pass:${kind}:${at}`, kind, at, reason }] : [];
}

function totalOf(pass: PrerenderPassView): number | null {
  const left = pass.pagesLeft;
  const done = pass.pagesWritten + pass.pagesSkipped + pass.pagesFailed;
  return left === null || left === undefined ? null : done + left;
}

function versionEvents(before: ActivityBefore, next: PrerenderPassProgress): ActivityEvent[] {
  const known = new Set(before.progress.versions.map((version) => version.version));
  const at = next.generatedAt;
  const joined: ActivityEvent[] = next.versions
    .filter((version) => !known.has(version.version))
    .map((version) => ({
      key: `joined:${version.version}`,
      kind: 'joined',
      at,
      version: version.version,
      units: [...version.cells].filter((code) => code !== NO_UNIT).length,
    }));
  const was = totalOf(before.progress.pass);
  const now = totalOf(next.pass);
  if (was === null || now === null || now <= was * TOTAL_STEP) {
    return joined;
  }
  const version = joined[0]?.version;
  const kind = version ? 'total_up_joined' : 'total_up';
  return [...joined, { key: `total:${at}`, kind, at, version, pages: now - was }];
}

/**
 * What an answer of the progress tells that the previous did not (§ 4.6): units that ended
 * (from `recent`, those not `seen` yet), versions that joined the pass, a total re-estimated
 * by more than 1 %, and the moves of the pass itself, newest first. At the first answer, or
 * for another pass, only the units that ended: the rest needs a previous answer to compare.
 * It gives the units seen too, the `seen` of the next call.
 */
export function activityEvents(
  before: ActivityBefore | undefined,
  next: PrerenderPassProgress,
  seen: ReadonlySet<string>,
): ActivityUpdate {
  const isSamePass = before !== undefined && before.progress.pass.id === next.pass.id;
  const units = next.recent
    .filter((unit) => !isSamePass || !seen.has(unitSeenKey(unit)))
    .map(unitEvent);
  const passEvents = isSamePass
    ? [
        ...stateEvent(before.progress.pass, next.pass, next.generatedAt),
        ...versionEvents(before, next),
      ]
    : [];
  const events = [...passEvents, ...units];
  return {
    events: events.sort((one, other) => Date.parse(other.at) - Date.parse(one.at)),
    seen: new Set(next.recent.map(unitSeenKey)),
  };
}
