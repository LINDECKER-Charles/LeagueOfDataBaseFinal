import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { getAnalyticsReport } from '../../../../../core/api/generated/fn/admin-analytics/get-analytics-report';
import { getRobotsReport } from '../../../../../core/api/generated/fn/admin-analytics/get-robots-report';
import { Heatmap } from '../../../charts/heatmap';
import { Sparkline } from '../../../charts/sparkline';
import { TimeSeriesChart } from '../../../charts/time-series-chart';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
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
import { totalOf } from '../robots/shared/robot-chart';
import { TrafficExplorer } from './explorer/traffic-explorer';
import { StatusTable } from './status-table';
import { trafficChart } from './traffic-chart';
import { trendOf } from './trend';

// An entity is counted as `{type}:{key}`: the page names it by its key alone.
const ENTITY_SEPARATOR = ':';

/**
 * `/admin/traffic`: the readers of the period, and the robots beside them. Their figures and
 * how they moved since the period before, the days of the views, the visitors and the robots'
 * hits, every page, entity and referrer to explore, what was viewed, in which language, with
 * which status, and the hours of the week.
 */
@Component({
  selector: 'lodb-traffic-panel',
  imports: [
    AdminCard,
    AdminRule,
    FigurePipe,
    Heatmap,
    Kpi,
    Legend,
    PageHead,
    PanelState,
    RankList,
    RouterLink,
    SegmentBar,
    Sparkline,
    StatusTable,
    TimeSeriesChart,
    TrafficExplorer,
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
  protected readonly robots = injectPanel(getRobotsReport, () => ({ range: this.query.range() }));
  protected readonly chart = computed(() =>
    trafficChart(this.report.value()?.series ?? [], this.robots.value(), this.texts),
  );
  /** The mean views of a day of the period, rounded as the legacy tile wrote it. */
  protected readonly perDay = computed(() => {
    const report = this.report.value();
    return report ? Math.round(report.totals.views / Math.max(report.days, 1)) : 0;
  });
  protected readonly viewsTrend = computed(() =>
    trendOf(this.report.value()?.totals.views ?? 0, this.report.value()?.previous?.views),
  );
  protected readonly visitorsTrend = computed(() =>
    trendOf(
      this.report.value()?.totals.uniqueVisitors ?? 0,
      this.report.value()?.previous?.uniqueVisitors,
    ),
  );
  /** The robots' share of every request of the period: theirs and the readers' views. */
  protected readonly robotShare = computed(() => {
    const hits = this.robots.value()?.totals.figures.hits ?? 0;
    const views = this.report.value()?.totals.views ?? 0;
    return hits + views > 0 ? hits / (hits + views) : 0;
  });
  protected readonly robotDays = computed(() => totalOf(this.robots.value()?.categorySeries ?? []));
  protected readonly types = computed(() =>
    rankRows(this.report.value()?.byType ?? [], (name) => this.texts.term('traffic.types', name)),
  );
  protected readonly kinds = computed(() =>
    rankRows(this.report.value()?.byKind ?? [], (name) => this.texts.term('traffic.kinds', name)),
  );
  protected readonly locales = computed(() => rankRows(this.report.value()?.locale ?? []));
  protected readonly langs = computed(() => rankRows(this.report.value()?.lang ?? []));
  protected readonly entities = computed(() =>
    rankRows(
      this.report.value()?.topEntities ?? [],
      (name) => name.split(ENTITY_SEPARATOR).at(-1) ?? name,
    ),
  );
}
