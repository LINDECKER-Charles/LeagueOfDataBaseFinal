import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { readAdminMonitoring } from '../../../../../core/api/generated/fn/admin-monitoring/read-admin-monitoring';
import { Button } from '../../../../../ui/controls/button';
import { duration } from '../../../format/duration';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { healthTone } from '../../../shared/health-tone';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminCard } from '../../../widgets/admin-card';
import { Badge } from '../../../widgets/badge';
import { Kpi } from '../../../widgets/kpi';
import { PageHead } from '../../../widgets/page-head';
import { RankList } from '../../../widgets/rank-list';
import { MonitoringVersions } from './monitoring-versions';

/**
 * `/admin/monitoring`: the health of the API. Its dependencies as probed, the figures of the
 * process, the queues of the ingestion, the Data Dragon versions, the heaviest tables and
 * what the community produced. The API caches the report; refresh probes again.
 */
@Component({
  selector: 'lodb-monitoring-panel',
  imports: [
    AdminCard,
    Badge,
    Button,
    FigurePipe,
    Kpi,
    MonitoringVersions,
    PageHead,
    PanelState,
    RankList,
    StampPipe,
    TranslocoPipe,
  ],
  templateUrl: './monitoring-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonitoringPanel {
  private readonly refreshes = signal(0);

  protected readonly tone = healthTone;
  protected readonly report = injectPanel(readAdminMonitoring, () =>
    this.refreshes() > 0 ? { refresh: true } : {},
  );
  protected readonly uptime = computed(() =>
    duration(this.report.value()?.process.uptimeSeconds ?? 0),
  );
  protected readonly tables = computed(() =>
    (this.report.value()?.tables ?? []).map((table) => ({ name: table.name, value: table.bytes })),
  );

  protected refresh(): void {
    this.refreshes.update((count) => count + 1);
  }
}
