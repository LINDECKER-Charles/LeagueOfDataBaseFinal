import type { PrerenderDemandReport } from '../../../../../../core/api/generated/models/prerender-demand-report';
import type { Tone } from '../../../../widgets/badge';

/** How the automatic queue runs: its badge, and the line that says why when it does not. */
export interface DemandStatus {
  /** Key under `admin.prerender.demand.states`. */
  readonly state: 'active' | 'off' | 'dormant' | 'blind';
  readonly tone: Tone;
  /** Key under `admin.prerender.demand.why`, null while it runs fully. */
  readonly why: 'off' | 'dormant' | 'blind' | null;
}

/**
 * The queue's state, from the most to the least blocking: switched off, the store not
 * configured, then active but without reading the SSR (it ends what it holds, nothing enters).
 */
export function demandStatusOf(report: PrerenderDemandReport): DemandStatus {
  if (!report.isEnabled) {
    return { state: 'off', tone: 'muted', why: 'off' };
  }
  if (!report.isActive) {
    return { state: 'dormant', tone: 'muted', why: 'dormant' };
  }
  return report.isCapturing
    ? { state: 'active', tone: 'good', why: null }
    : { state: 'blind', tone: 'warn', why: 'blind' };
}
