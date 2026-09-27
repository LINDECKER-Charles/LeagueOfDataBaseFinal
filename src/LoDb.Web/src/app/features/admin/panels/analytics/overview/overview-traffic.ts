import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { getAnalyticsReport } from '../../../../../core/api/generated/fn/admin-analytics/get-analytics-report';
import { Chip } from '../../../../../ui/controls/chip';
import { categorySlices } from '../../../charts/category-slices';
import { DonutChart } from '../../../charts/donut-chart';
import { Sparkline } from '../../../charts/sparkline';
import { TimeSeriesChart } from '../../../charts/time-series-chart';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { Kpi } from '../../../widgets/kpi';
import { Legend } from '../../../widgets/legend';
import { RankList } from '../../../widgets/rank-list';
import { rankRows } from '../rank-rows';
import { trafficSeries } from '../traffic-series';

/**
 * The traffic of the overview, the legacy `overview-traffic` panel, for the period of the
 * URL: its views, its visitors and its most viewed page, their days, the resources viewed,
 * the pages most viewed and where the visitors came from, each linking to its page. A new
 * `revision` (a consolidation just ran) reads the report again.
 */
@Component({
  selector: 'lodb-overview-traffic',
  imports: [
    AdminCard,
    Chip,
    DonutChart,
    FigurePipe,
    Kpi,
    Legend,
    PanelState,
    RankList,
    RouterLink,
    Sparkline,
    TimeSeriesChart,
    AdminTextPipe,
  ],
  templateUrl: './overview-traffic.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverviewTraffic {
  /** Bumped once the raw events were consolidated, which changes the report. */
  readonly revision = input(0);

  private readonly texts = injectAdminText();
  private readonly query = injectQuery();

  protected readonly report = injectPanel(getAnalyticsReport, () => ({
    range: this.query.range(),
  }));
  protected readonly traffic = computed(() =>
    trafficSeries(this.report.value()?.series ?? [], this.texts),
  );
  protected readonly resources = computed(() =>
    categorySlices('resource', rankRows(this.report.value()?.byType ?? []), (type) =>
      this.texts.term('traffic.types', type),
    ),
  );
  protected readonly pages = computed(() => rankRows(this.report.value()?.topPages ?? []));
  // Named by their key, as the legacy overview listed them.
  protected readonly sources = computed(() => rankRows(this.report.value()?.refSource ?? []));

  constructor() {
    effect(() => {
      if (this.revision() > 0) {
        untracked(() => this.report.reload());
      }
    });
  }
}
