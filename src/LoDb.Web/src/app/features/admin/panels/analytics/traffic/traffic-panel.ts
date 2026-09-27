import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { getAnalyticsReport } from '../../../../../core/api/generated/fn/admin-analytics/get-analytics-report';
import { rollupAnalytics } from '../../../../../core/api/generated/fn/admin-analytics/rollup-analytics';
import { Button } from '../../../../../ui/controls/button';
import { DonutChart } from '../../../charts/donut-chart';
import { Heatmap } from '../../../charts/heatmap';
import { paletteSlices } from '../../../charts/palette-slices';
import { TimeSeriesChart } from '../../../charts/time-series-chart';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminCommand } from '../../../shared/http/admin-command';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminCard } from '../../../widgets/admin-card';
import { Kpi } from '../../../widgets/kpi';
import { Legend } from '../../../widgets/legend';
import { PageHead } from '../../../widgets/page-head';
import { RangeBar } from '../../../widgets/range-bar';
import { RankList } from '../../../widgets/rank-list';
import { rankRows } from '../rank-rows';
import { trafficSeries } from '../traffic-series';

// The rankings a long report folds after this many rows.
const RANK_LIMIT = 10;

/**
 * `/admin/traffic`: the page views of the period, day by day and hour by hour, the pages
 * and entities most viewed, the answers served. "Consolidate" rolls the raw events of the
 * last days into the daily aggregates the report reads, as the nightly job does.
 */
@Component({
  selector: 'lodb-traffic-panel',
  imports: [
    AdminCard,
    Button,
    DonutChart,
    FigurePipe,
    Heatmap,
    Kpi,
    Legend,
    PageHead,
    PanelState,
    RangeBar,
    RankList,
    TimeSeriesChart,
    TranslocoPipe,
  ],
  templateUrl: './traffic-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrafficPanel {
  private readonly texts = injectAdminText();
  private readonly command = inject(AdminCommand);

  protected readonly query = injectQuery();
  protected readonly report = injectPanel(getAnalyticsReport, () => ({
    range: this.query.range(),
  }));
  protected readonly rollingUp = signal(false);
  protected readonly limit = RANK_LIMIT;
  protected readonly traffic = computed(() =>
    trafficSeries(this.report.value()?.series ?? [], this.texts),
  );
  protected readonly types = computed(() =>
    paletteSlices(
      rankRows(this.report.value()?.byType ?? [], (name) => this.texts.term('traffic.types', name)),
      this.texts.text('common.others'),
    ),
  );
  protected readonly kinds = computed(() =>
    rankRows(this.report.value()?.byKind ?? [], (name) => this.texts.term('traffic.kinds', name)),
  );
  protected readonly pages = computed(() => rankRows(this.report.value()?.topPages ?? []));
  protected readonly entities = computed(() => rankRows(this.report.value()?.topEntities ?? []));
  protected readonly statuses = computed(() => rankRows(this.report.value()?.status ?? []));
  protected readonly routes = computed(() => rankRows(this.report.value()?.byRoute ?? []));

  protected async rollUp(): Promise<void> {
    this.rollingUp.set(true);
    const receipt = await this.command.run(rollupAnalytics, {}, (done) => ({
      key: 'traffic.rolled_up',
      params: { count: done.days.length },
    }));
    this.rollingUp.set(false);
    if (receipt !== null) {
      this.report.reload();
    }
  }
}
