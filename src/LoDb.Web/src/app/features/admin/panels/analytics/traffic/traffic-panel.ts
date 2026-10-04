import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { getAnalyticsReport } from '../../../../../core/api/generated/fn/admin-analytics/get-analytics-report';
import { Heatmap } from '../../../charts/heatmap';
import { Sparkline } from '../../../charts/sparkline';
import { TimeSeriesChart } from '../../../charts/time-series-chart';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { RANGE_SEGMENTS } from '../../../shared/range-segments';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { Kpi } from '../../../widgets/kpi';
import { Legend } from '../../../widgets/legend';
import { RankList } from '../../../widgets/rank-list';
import { SegmentBar } from '../../../widgets/segment-bar';
import { rankRows } from '../rank-rows';
import { trafficSeries } from '../traffic-series';
import { StatusTable } from './status-table';

// An entity is counted as `{type}:{key}`: the page names it by its key alone.
const ENTITY_SEPARATOR = ':';

/**
 * `/admin/traffic`: the page views of the period, in the order of the legacy page. Their
 * figures, their days, the pages and entities most viewed, the resources, the kinds of page
 * and the statuses served, then the hours of the week.
 */
@Component({
  selector: 'lodb-traffic-panel',
  imports: [
    AdminCard,
    FigurePipe,
    Heatmap,
    Kpi,
    Legend,
    PageHead,
    PanelState,
    RankList,
    SegmentBar,
    Sparkline,
    StatusTable,
    TimeSeriesChart,
    AdminTextPipe,
  ],
  templateUrl: './traffic-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrafficPanel {
  private readonly texts = injectAdminText();

  protected readonly query = injectQuery();
  protected readonly ranges = RANGE_SEGMENTS;
  protected readonly report = injectPanel(getAnalyticsReport, () => ({
    range: this.query.range(),
  }));
  protected readonly traffic = computed(() =>
    trafficSeries(this.report.value()?.series ?? [], this.texts),
  );
  /** The mean views of a day of the period, rounded as the legacy tile wrote it. */
  protected readonly perDay = computed(() => {
    const report = this.report.value();
    return report ? Math.round(report.totals.views / Math.max(report.days, 1)) : 0;
  });
  protected readonly types = computed(() =>
    rankRows(this.report.value()?.byType ?? [], (name) => this.texts.term('traffic.types', name)),
  );
  protected readonly kinds = computed(() =>
    rankRows(this.report.value()?.byKind ?? [], (name) => this.texts.term('traffic.kinds', name)),
  );
  protected readonly pages = computed(() => rankRows(this.report.value()?.topPages ?? []));
  protected readonly entities = computed(() =>
    rankRows(
      this.report.value()?.topEntities ?? [],
      (name) => name.split(ENTITY_SEPARATOR).at(-1) ?? name,
    ),
  );
}
