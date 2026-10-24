import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import type { AdminText } from '../../../../../shared/inject-admin-text';
import type { StatusTile } from '../../../../../widgets/status-tabs/status-tile';
import { monthlyCost } from '../../../backups/monthly-cost';
import { decimalBytes } from '../../format/decimal-bytes';

const STORAGE = 'prerender.overview.views.storage';

/** The tile of the storage: what the store holds, what it costs a month, its pages. */
export function storageTileOf(report: PrerenderReport, texts: AdminText): Omit<StatusTile, 'key'> {
  const storage = report.storage;
  return {
    label: texts.text(`${STORAGE}.label`),
    value: decimalBytes(storage.bytes),
    sub: texts.text(`${STORAGE}.sub`, {
      cost: monthlyCost(storage.monthlyCost, storage.currency),
      pages: storage.pages,
    }),
    tone: null,
  };
}
