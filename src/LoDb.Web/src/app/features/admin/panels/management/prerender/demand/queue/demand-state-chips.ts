import type { PrerenderDemandReport } from '../../../../../../../core/api/generated/models/prerender-demand-report';
import { figure } from '../../../../../format/figure';

/** A state of a page of the queue, as the API spells it. */
export type DemandState = 'pending' | 'rendering' | 'done' | 'failed' | 'skipped';

/** A filter of the list: a state, and how many pages are in it. */
export interface DemandStateChip {
  readonly state: DemandState;
  /** Written for reading: `1 830`. */
  readonly count: string;
}

const STATES: readonly DemandState[] = ['pending', 'rendering', 'done', 'failed', 'skipped'];

/** The states of the queue in the order its pages go through them, each with its count. */
export function demandStateChips(report: PrerenderDemandReport): DemandStateChip[] {
  return STATES.map((state) => ({ state, count: figure(report[state]) }));
}
