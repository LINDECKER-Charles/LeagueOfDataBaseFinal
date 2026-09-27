import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { readAdminMonitoring } from '../../../../../core/api/generated/fn/admin-monitoring/read-admin-monitoring';
import { Button } from '../../../../../ui/controls/button';
import { Chip } from '../../../../../ui/controls/chip';
import { figure } from '../../../format/figure';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminBand } from '../../../layout/admin-band';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { Kpi } from '../../../widgets/kpi';
import { MonitoringRuntime } from './monitoring-runtime';
import { ProbeCard } from './probe-card';

/**
 * `/admin/monitoring`: the health of the site, in the order of the legacy page. Its probes as
 * cards, the figures of the application, the volumes (the heaviest tables, the e-mails
 * waiting), then what the new API adds: its process, the ingestion and the Data Dragon
 * versions. The API caches the report; "Rafraîchir" probes again.
 */
@Component({
  selector: 'lodb-monitoring-panel',
  imports: [
    AdminBand,
    AdminCard,
    AdminRule,
    Button,
    Chip,
    FigurePipe,
    Kpi,
    MonitoringRuntime,
    PageHead,
    PanelState,
    ProbeCard,
    StampPipe,
    AdminTextPipe,
  ],
  templateUrl: './monitoring-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonitoringPanel {
  private readonly refreshes = signal(0);

  protected readonly report = injectPanel(readAdminMonitoring, () =>
    this.refreshes() > 0 ? { refresh: true } : {},
  );

  /** A count of the outbox, a dash when it could not be read. */
  protected outbox(count: number | null | undefined): string {
    return count === null || count === undefined ? '—' : figure(count);
  }

  protected refresh(): void {
    this.refreshes.update((count) => count + 1);
  }
}
