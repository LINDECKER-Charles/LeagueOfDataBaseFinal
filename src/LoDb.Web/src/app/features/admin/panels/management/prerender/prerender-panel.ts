import { ChangeDetectionStrategy, Component, computed, linkedSignal } from '@angular/core';
import { readAdminPrerender } from '../../../../../core/api/generated/fn/admin-prerender/read-admin-prerender';
import type { PassRequest } from '../../../../../core/api/generated/models/pass-request';
import type { PrerenderReport } from '../../../../../core/api/generated/models/prerender-report';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminBand } from '../../../layout/admin-band';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { Kpi } from '../../../widgets/kpi';
import { monthlyCost } from '../backups/monthly-cost';
import { DecimalBytesPipe } from './format/decimal-bytes-pipe';
import { injectPassActions } from './inject-pass-actions';
import { LaunchForm, type LaunchLock } from './launch/launch-form';
import { CurrentPassCard } from './pass/current-pass-card';
import { PassHistoryTable } from './pass/pass-history-table';
import { pollWhile } from './poll-while';
import { StorageSection } from './storage/storage-section';

const RUNNING = 'running';
const RETIRED = 'retired';

/**
 * `/admin/prerender`: the pre-rendering of the older patches into the object store. The key
 * figures (the epoch nginx serves, the active pass, what the store holds and its estimated
 * monthly cost), the notice to switch the served epoch once a newer one is complete (a
 * variable and a redeploy, never from here), the active pass with its pause, resume and
 * cancel, the launch of a pass, the store epoch by epoch and version by version, then the
 * latest passes. It reads itself again every few seconds while a pass runs.
 */
@Component({
  selector: 'lodb-prerender-panel',
  imports: [
    AdminBand,
    AdminCard,
    AdminRule,
    CurrentPassCard,
    DecimalBytesPipe,
    FigurePipe,
    Kpi,
    LaunchForm,
    PageHead,
    PanelState,
    PassHistoryTable,
    StorageSection,
    AdminTextPipe,
  ],
  templateUrl: './prerender-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrerenderPanel {
  protected readonly report = injectPanel(readAdminPrerender, () => ({}));
  protected readonly actions = injectPassActions(() => this.report.reload());
  /** Why no pass may be launched now, null when one may. */
  protected readonly lock = computed<LaunchLock | null>(() => {
    const report = this.report.value();
    if (report && !report.isConfigured) {
      return 'locked_dormant';
    }
    return report?.current ? 'locked_active' : null;
  });
  protected readonly cost = computed(() => {
    const storage = this.report.value()?.storage;
    return storage ? monthlyCost(storage.monthlyCost, storage.currency) : '';
  });
  /** What the retired epochs still cost until the lifecycle deletes them, '' when none. */
  protected readonly retiredCost = computed(() => {
    const storage = this.report.value()?.storage;
    const retired = storage?.epochs.filter((epoch) => epoch.state === RETIRED) ?? [];
    const amount = retired.reduce((sum, epoch) => sum + epoch.monthlyCost, 0);
    return storage && retired.length > 0 ? monthlyCost(amount, storage.currency) : '';
  });
  // A failed read clears the report, but not what it last said: a 502 while the API
  // redeploys must not end the polling of a pass that runs for days.
  private readonly isRunning = linkedSignal<PrerenderReport | undefined, boolean>({
    source: this.report.value,
    computation: (report, previous) =>
      report ? report.current?.state === RUNNING : (previous?.value ?? false),
  });

  constructor() {
    pollWhile(this.isRunning, this.report);
  }

  protected launch(request: PassRequest): void {
    void this.actions.start(request);
  }

  protected pause(id: number): void {
    void this.actions.pause(id);
  }

  protected resume(id: number): void {
    void this.actions.resume(id);
  }

  protected cancel(id: number): void {
    void this.actions.cancel(id);
  }
}
