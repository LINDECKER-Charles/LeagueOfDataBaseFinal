import type { PrerenderPassProgress } from '../../../../../../../core/api/generated/models/prerender-pass-progress';
import type { FailingUnit } from '../now/failing-unit-of';
import type { Stall, StallCause } from '../now/stall-of';

/** What goes wrong with a pass, as the monitor's one status says it. */
export type Alarm =
  | { readonly kind: 'failing'; readonly version: string; readonly locale: string }
  | { readonly kind: 'stalled'; readonly cause: StallCause }
  | { readonly kind: 'worker_off' }
  | { readonly kind: 'ssr' };

const RUNNING = 'running';

/**
 * The first thing going wrong with a pass (§ 4.5), for the status a screen reader hears: a
 * unit failing in numbers, a stall and its cause, the background task off, the SSR out of
 * reach. Stable while the same thing goes wrong: nothing that moves each second (a duration,
 * a count) is in it, so that it is said once, when it starts. Null while all is well.
 */
export function alarmOf(
  progress: PrerenderPassProgress | undefined,
  stall: Stall | null,
  failing: FailingUnit | null,
): Alarm | null {
  if (failing) {
    return { kind: 'failing', version: failing.unit.version, locale: failing.unit.locale };
  }
  if (stall?.kind === 'stalled') {
    return { kind: 'stalled', cause: stall.cause };
  }
  if (progress && !progress.pulse.isWorkerEnabled) {
    return { kind: 'worker_off' };
  }
  return progress?.pass.state === RUNNING && progress.pulse.ssrUnreachableSince
    ? { kind: 'ssr' }
    : null;
}
