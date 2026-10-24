import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { figure } from '../../../../../format/figure';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { StatusTile } from '../../../../../widgets/status-tabs/status-tile';

const COVERAGE = 'prerender.overview.views.coverage';
const NONE = '—';

/**
 * The tile of the coverage: what the served epoch covers of the units of the older patches,
 * the epoch and its complete patches; a green dot, and its number, once a newer epoch is
 * ready to be served.
 */
export function coverageTileOf(report: PrerenderReport, texts: AdminText): Omit<StatusTile, 'key'> {
  const served = report.storage.epochs.find((epoch) => epoch.epoch === report.servedEpoch);
  const coverage = served?.coverage;
  const ready = report.readyEpoch ?? null;
  const epoch = report.servedEpoch;
  const counted = coverage && coverage.expectedUnits > 0 ? coverage : null;
  let sub = texts.text(`${COVERAGE}.sub_none`, { epoch });
  if (ready !== null) {
    sub = texts.text(`${COVERAGE}.sub_ready`, { epoch, ready });
  } else if (counted) {
    const complete = figure(counted.completeVersions);
    sub = texts.text(`${COVERAGE}.sub`, { epoch, complete, total: counted.pinnedVersions });
  }
  return {
    label: texts.text(`${COVERAGE}.label`),
    value: counted ? figure(counted.completeUnits / counted.expectedUnits, 'share') : NONE,
    sub,
    tone: ready === null ? null : 'good',
  };
}
