import type { PrerenderDemandReport } from '../../../../../../../core/api/generated/models/prerender-demand-report';
import { figure } from '../../../../../format/figure';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import { decimalBytes } from '../../format/decimal-bytes';

const QUEUE = 'prerender.demand.queue';

/**
 * What the queue did in the served epoch, in one sentence: the pages it wrote there, those it
 * still holds and their weight, then those a pass wrote again since, which count among the
 * passes' pages.
 */
export function demandEpochLine(report: PrerenderDemandReport, texts: AdminText): string {
  const { epoch, servedEpochPages: kept, superseded } = report;
  const written = kept + superseded;
  if (written === 0) {
    return texts.text(`${QUEUE}.epoch_empty`, { epoch });
  }
  const line = texts.text(`${QUEUE}.epoch`, {
    epoch,
    written,
    writtenText: figure(written),
    kept,
    keptText: figure(kept),
    size: decimalBytes(report.servedEpochBytes),
  });
  if (superseded === 0) {
    return line;
  }
  const rewritten = texts.text(`${QUEUE}.epoch_superseded`, {
    superseded,
    supersededText: figure(superseded),
  });
  return `${line} ${rewritten}`;
}
