import { type Signal, computed } from '@angular/core';
import { readAdminScalewayBill } from '../../../../../core/api/generated/fn/admin-scaleway/read-admin-scaleway-bill';
import type { ScalewayBillReport } from '../../../../../core/api/generated/models/scaleway-bill-report';
import { injectPanel } from '../../../state/inject-panel';
import type { Panel } from '../../../state/panel';
import { type BillProject, type BillView, billViewOf } from './bill-view';

/** The Scaleway bill as a block reads it: the report loaded on its own, and one project's view. */
export interface ScalewayBill {
  readonly report: Panel<ScalewayBillReport>;
  /** The bill of the project, undefined until the report arrives or once its load failed. */
  readonly view: Signal<BillView | undefined>;
}

/**
 * Loads the Scaleway bill apart from the panel that shows it: the API reads Scaleway on
 * demand, a few seconds the first time, and the panel must never wait on that.
 */
export function injectScalewayBill(project: () => BillProject): ScalewayBill {
  const report = injectPanel(readAdminScalewayBill, () => ({}));
  const view = computed(() => {
    const value = report.value();
    return value ? billViewOf(value, project()) : undefined;
  });
  return { report, view };
}
