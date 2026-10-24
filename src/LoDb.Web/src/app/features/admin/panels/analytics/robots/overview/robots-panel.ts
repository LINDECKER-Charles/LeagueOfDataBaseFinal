import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { getRobotsReport } from '../../../../../../core/api/generated/fn/admin-analytics/get-robots-report';
import type { AnalyticsRank } from '../../../../../../core/api/generated/models/analytics-rank';
import { DonutChart } from '../../../../charts/donut-chart';
import { Sparkline } from '../../../../charts/sparkline';
import { TimeSeriesChart } from '../../../../charts/time-series-chart';
import { FigurePipe } from '../../../../format/figure-pipe';
import { StampPipe } from '../../../../format/stamp-pipe';
import { AdminCard } from '../../../../layout/admin-card';
import { PageHead } from '../../../../layout/page-head';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { injectQuery } from '../../../../shared/inject-query';
import { RANGE_SEGMENTS } from '../../../../shared/range-segments';
import { injectPanel } from '../../../../state/inject-panel';
import { PanelState } from '../../../../state/panel-state';
import { Kpi } from '../../../../widgets/kpi';
import { Legend } from '../../../../widgets/legend';
import { RankList, type RankRow } from '../../../../widgets/rank-list';
import { SegmentBar } from '../../../../widgets/segment-bar';
import { categoryChart, totalOf } from '../shared/robot-chart';
import { categoryColor } from '../shared/robot-palette';
import { RobotTabs } from '../shared/robot-tabs';
import { RobotsTable } from './robots-table';

/**
 * `/admin/robots`: every robot nginx answered over the period, the hidden ones its pace gave
 * away included. Their figures, their days by category, what they are, what they asked and
 * how they were answered, from which networks, and the table of every robot, each opening
 * its own page.
 */
@Component({
  selector: 'lodb-robots-panel',
  imports: [
    AdminCard,
    DonutChart,
    FigurePipe,
    Kpi,
    Legend,
    PageHead,
    PanelState,
    RankList,
    RobotTabs,
    RobotsTable,
    SegmentBar,
    Sparkline,
    StampPipe,
    TimeSeriesChart,
    AdminTextPipe,
  ],
  templateUrl: './robots-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RobotsPanel {
  private readonly texts = injectAdminText();

  protected readonly query = injectQuery();
  protected readonly ranges = RANGE_SEGMENTS;
  protected readonly report = injectPanel(getRobotsReport, () => ({ range: this.query.range() }));
  protected readonly chart = computed(() =>
    categoryChart(this.report.value()?.categorySeries ?? [], this.texts),
  );
  protected readonly daily = computed(() => totalOf(this.report.value()?.categorySeries ?? []));
  protected readonly categories = computed(() =>
    (this.report.value()?.categories ?? []).map((share) => ({
      name: this.texts.term('robots.categories', share.category),
      value: share.hits,
      color: categoryColor(share.category),
    })),
  );
  protected readonly statuses = computed(() => this.rows(this.report.value()?.statuses));
  protected readonly sections = computed(() =>
    this.rows(this.report.value()?.sections, 'robots.sections'),
  );
  protected readonly verdicts = computed(() =>
    this.rows(this.report.value()?.verdicts, 'robots.verdicts'),
  );
  /** The share of the hits whose network proved the robot, from 0 to 1. */
  protected readonly verifiedShare = computed(() => {
    const figures = this.report.value()?.totals.figures;
    return figures && figures.hits > 0 ? figures.verified / figures.hits : 0;
  });

  private rows(ranks: readonly AnalyticsRank[] | undefined, terms?: string): RankRow[] {
    return (ranks ?? []).map((rank) => ({
      name: terms ? this.texts.term(terms, rank.name) : rank.name,
      value: rank.count,
    }));
  }
}
