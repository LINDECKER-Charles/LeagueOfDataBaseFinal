import type { PrerenderDemandReport } from '../../../../../../../core/api/generated/models/prerender-demand-report';
import type { PrerenderRevisionReport } from '../../../../../../../core/api/generated/models/prerender-revision-report';
import { figure } from '../../../../../format/figure';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { StatusTile } from '../../../../../widgets/status-tabs/status-tile';
import { demandStatusOf } from '../../demand/demand-status-of';

/** What the tile of the automatic queues reads: each report, once read. */
export interface QueuesTileSource {
  readonly demand: PrerenderDemandReport | undefined;
  readonly revision: PrerenderRevisionReport | undefined;
}

const QUEUES = 'prerender.overview.views.queues';
const NONE = '—';

function count(report: { readonly pending: number } | undefined): string {
  return report ? figure(report.pending) : NONE;
}

// Gold when the queue of the refused pages renders without reading them, grey when a queue
// sleeps, a live dot while it renders, nothing while all waits quietly.
function toneOf({ demand, revision }: QueuesTileSource): StatusTile['tone'] {
  const state = demand ? demandStatusOf(demand).state : null;
  if (state === 'blind') {
    return 'warn';
  }
  if (state === 'off' || state === 'dormant' || revision?.isActive === false) {
    return 'muted';
  }
  return (demand?.rendering ?? 0) > 0 ? 'live' : null;
}

/**
 * The tile of the automatic queues: the pages they hold, the queue of the pages the SSR
 * refused and that of the closed states each, and how they run.
 */
export function queuesTileOf(source: QueuesTileSource, texts: AdminText): Omit<StatusTile, 'key'> {
  const { demand, revision } = source;
  const isRead = demand !== undefined || revision !== undefined;
  const pending = (demand?.pending ?? 0) + (revision?.pending ?? 0);
  return {
    label: texts.text(`${QUEUES}.label`),
    value: isRead ? figure(pending) : NONE,
    sub: texts.text(`${QUEUES}.sub`, { demand: count(demand), revision: count(revision) }),
    tone: toneOf(source),
  };
}
