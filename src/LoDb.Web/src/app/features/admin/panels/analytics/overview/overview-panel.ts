import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { getAnalyticsReport } from '../../../../../core/api/generated/fn/admin-analytics/get-analytics-report';
import { readAdminMonitoring } from '../../../../../core/api/generated/fn/admin-monitoring/read-admin-monitoring';
import { readAdminStorage } from '../../../../../core/api/generated/fn/admin-storage/read-admin-storage';
import { Sparkline } from '../../../charts/sparkline';
import { TimeSeriesChart } from '../../../charts/time-series-chart';
import { FigurePipe } from '../../../format/figure-pipe';
import { healthTone } from '../../../shared/health-tone';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminCard } from '../../../widgets/admin-card';
import { Badge } from '../../../widgets/badge';
import { Kpi } from '../../../widgets/kpi';
import { Legend } from '../../../widgets/legend';
import { PageHead } from '../../../widgets/page-head';
import { RangeBar } from '../../../widgets/range-bar';
import { trafficSeries } from '../traffic-series';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';

/**
 * `/admin`: the site at a glance. The traffic of the period, the health of the services and
 * the figures of the application, the size of the storage; three reports loaded side by
 * side, each failing on its own.
 */
@Component({
  selector: 'lodb-overview-panel',
  imports: [
    AdminCard,
    Badge,
    FigurePipe,
    Kpi,
    Legend,
    PageHead,
    PanelState,
    RangeBar,
    RouterLink,
    Sparkline,
    TimeSeriesChart,
    AdminTextPipe,
  ],
  templateUrl: './overview-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverviewPanel {
  private readonly texts = injectAdminText();

  protected readonly query = injectQuery();
  protected readonly report = injectPanel(getAnalyticsReport, () => ({
    range: this.query.range(),
  }));
  protected readonly monitoring = injectPanel(readAdminMonitoring, () => ({}));
  protected readonly storage = injectPanel(readAdminStorage, () => ({}));
  protected readonly traffic = computed(() =>
    trafficSeries(this.report.value()?.series ?? [], this.texts),
  );
  protected readonly healthTone = healthTone;
}
