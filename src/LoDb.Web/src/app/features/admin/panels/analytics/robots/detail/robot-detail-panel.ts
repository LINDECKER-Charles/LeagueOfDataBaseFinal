import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { getRobotDetail } from '../../../../../../core/api/generated/fn/admin-analytics/get-robot-detail';
import type { AnalyticsRank } from '../../../../../../core/api/generated/models/analytics-rank';
import { Button } from '../../../../../../ui/controls/button';
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
import { Badge } from '../../../../widgets/badge';
import { Kpi } from '../../../../widgets/kpi';
import { Legend } from '../../../../widgets/legend';
import { RankList, type RankRow } from '../../../../widgets/rank-list';
import { SegmentBar } from '../../../../widgets/segment-bar';
import { robotLabel, robotOperator } from '../shared/robot-label';
import { categoryColor, statusTone } from '../shared/robot-palette';
import { RobotHitsTable } from '../shared/robot-hits-table';
import { statusChart } from '../shared/robot-chart';

const KEY = 'key';
const NOT_FOUND = 404;

/**
 * `/admin/robots/:key`: one robot over the period. Who it is and how it is checked, its
 * figures, its days by class of status, what it asked, from which networks and with which
 * user agents, the paths it asked the most and its latest hits. A robot unseen over the
 * period says so rather than fail.
 */
@Component({
  selector: 'lodb-robot-detail-panel',
  imports: [
    AdminCard,
    Badge,
    Button,
    FigurePipe,
    Kpi,
    Legend,
    PageHead,
    PanelState,
    RankList,
    RobotHitsTable,
    RouterLink,
    SegmentBar,
    StampPipe,
    TimeSeriesChart,
    AdminTextPipe,
  ],
  templateUrl: './robot-detail-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RobotDetailPanel {
  private readonly texts = injectAdminText();

  protected readonly query = injectQuery();
  protected readonly ranges = RANGE_SEGMENTS;
  protected readonly key = toSignal(
    inject(ActivatedRoute).paramMap.pipe(map((params) => params.get(KEY) ?? '')),
    { initialValue: '' },
  );
  protected readonly detail = injectPanel(getRobotDetail, () => ({
    key: this.key(),
    range: this.query.range(),
  }));
  protected readonly unseen = computed(() => this.detail.failure()?.status === NOT_FOUND);
  protected readonly name = computed(() => {
    const robot = this.detail.value()?.robot;
    return robot ? robotLabel(robot, this.texts) : this.key();
  });
  protected readonly operator = computed(() => {
    const robot = this.detail.value()?.robot;
    return robot ? robotOperator(robot) : '';
  });
  protected readonly chart = computed(() => statusChart(this.detail.value()?.statusSeries ?? []));
  protected readonly statuses = computed(() => this.rows(this.detail.value()?.statuses));
  protected readonly sections = computed(() =>
    this.rows(this.detail.value()?.sections, 'robots.sections'),
  );
  protected readonly verdicts = computed(() =>
    this.rows(this.detail.value()?.verdicts, 'robots.verdicts'),
  );
  protected readonly verifiedShare = computed(() => {
    const figures = this.detail.value()?.robot.figures;
    return figures && figures.hits > 0 ? figures.verified / figures.hits : 0;
  });

  protected readonly color = categoryColor;
  protected readonly statusTone = statusTone;

  private rows(ranks: readonly AnalyticsRank[] | undefined, terms?: string): RankRow[] {
    return (ranks ?? []).map((rank) => ({
      name: terms ? this.texts.term(terms, rank.name) : rank.name,
      value: rank.count,
    }));
  }
}
