import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { stamp } from '../../../../../format/stamp';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { StatusTile } from '../../../../../widgets/status-tabs/status-tile';

const HISTORY = 'prerender.overview.views.history';

/** The tile of the history: the passes the report lists, and how the newest one stands. */
export function historyTileOf(report: PrerenderReport, texts: AdminText): Omit<StatusTile, 'key'> {
  const [newest] = report.passes;
  const sub = newest
    ? texts.text(`${HISTORY}.sub`, {
        id: newest.id,
        state: texts.term('prerender.states', newest.state),
        date: stamp(newest.finishedAt ?? newest.createdAt, 'date'),
      })
    : texts.text(`${HISTORY}.sub_none`);
  return {
    label: texts.text(`${HISTORY}.label`),
    value: texts.text(`${HISTORY}.value`, { count: report.passes.length }),
    sub,
    tone: null,
  };
}
