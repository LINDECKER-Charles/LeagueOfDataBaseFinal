import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';

/** The likely cause of a pass that renders nothing, each a `live.stall.*` key. */
export type StallCause = 'ssr' | 'worker_off' | 'waiting' | 'remote' | 'unknown';

/** A running pass that does not move: just opened, or stalled for a cause. */
export type Stall =
  | { readonly kind: 'starting' }
  | { readonly kind: 'stalled'; readonly cause: StallCause; readonly idleSeconds: number };

const RUNNING = 'running';
const SECOND_MS = 1_000;
// The worker takes a pass at its next tick, 30 seconds at most (PrerenderWorker.Period).
const STARTING_MS = 90_000;
const STALLED_MS = 180_000;
// The engine beats at each round: older than this, no instance here writes the pass.
const PULSE_STALE_MS = 120_000;

function lastActivity(progress: PrerenderPassProgress): number {
  const stamps = [
    ...progress.running.map((unit) => unit.updatedAt),
    progress.recent[0]?.updatedAt,
    progress.pass.createdAt,
  ];
  return Math.max(...stamps.filter((at) => at !== undefined).map((at) => Date.parse(at)));
}

function causeOf(progress: PrerenderPassProgress, serverNow: number): StallCause {
  const { pulse, running } = progress;
  if (pulse.ssrUnreachableSince) {
    return 'ssr';
  }
  if (!pulse.isWorkerEnabled) {
    return 'worker_off';
  }
  if (pulse.waiting.length > 0 && running.length === 0) {
    return 'waiting';
  }
  const observed = pulse.observedAt ? Date.parse(pulse.observedAt) : null;
  return observed === null || serverNow - observed > PULSE_STALE_MS ? 'remote' : 'unknown';
}

/**
 * Whether a running pass is stuck (§ 4.5): opened less than 90 seconds ago with no unit
 * started, it waits for the worker; with no page for 3 minutes, it is stalled, and the first
 * true cause says why. Never for a paused pass: a stopped unit does not move by design.
 */
export function stallOf(progress: PrerenderPassProgress, serverNow: number): Stall | null {
  const { pass } = progress;
  if (pass.state !== RUNNING) {
    return null;
  }
  const { completed, failed, running } = pass.units;
  const isUntouched = completed + failed + running === 0 && progress.running.length === 0;
  if (isUntouched && serverNow - Date.parse(pass.createdAt) < STARTING_MS) {
    return { kind: 'starting' };
  }
  const idle = serverNow - lastActivity(progress);
  if (idle < STALLED_MS) {
    return null;
  }
  return {
    kind: 'stalled',
    cause: causeOf(progress, serverNow),
    idleSeconds: Math.floor(idle / SECOND_MS),
  };
}
