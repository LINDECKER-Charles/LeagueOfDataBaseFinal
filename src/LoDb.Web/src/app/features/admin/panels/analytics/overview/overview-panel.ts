import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { rollupAnalytics } from '../../../../../core/api/generated/fn/admin-analytics/rollup-analytics';
import { Button } from '../../../../../ui/controls/button';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { AdminCommand } from '../../../shared/http/admin-command';
import { injectQuery } from '../../../shared/inject-query';
import { RANGE_SEGMENTS } from '../../../shared/range-segments';
import { SegmentBar } from '../../../widgets/segment-bar';
import { OverviewApp } from './overview-app';
import { OverviewStorage } from './overview-storage';
import { OverviewTraffic } from './overview-traffic';

/**
 * `/admin`: the site at a glance, in the order of the legacy console. The figures of the
 * application and the services, the traffic of the period, the storage: three reports loaded
 * side by side, each failing on its own. "Consolidate" rolls the raw events of the last days
 * into the daily aggregates the reports read, as the nightly job does.
 */
@Component({
  selector: 'lodb-overview-panel',
  imports: [
    AdminRule,
    Button,
    OverviewApp,
    OverviewStorage,
    OverviewTraffic,
    PageHead,
    SegmentBar,
    AdminTextPipe,
  ],
  templateUrl: './overview-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverviewPanel {
  private readonly command = inject(AdminCommand);

  protected readonly query = injectQuery();
  protected readonly ranges = RANGE_SEGMENTS;
  protected readonly rollingUp = signal(false);
  /** Bumped after each consolidation, for the traffic to read its report again. */
  protected readonly rolledUp = signal(0);

  protected async rollUp(): Promise<void> {
    this.rollingUp.set(true);
    try {
      const receipt = await this.command.run(rollupAnalytics, {}, (done) => ({
        key: 'overview.rolled_up',
        params: { count: done.days.length },
      }));
      if (receipt !== null) {
        this.rolledUp.update((count) => count + 1);
      }
    } finally {
      this.rollingUp.set(false);
    }
  }
}
