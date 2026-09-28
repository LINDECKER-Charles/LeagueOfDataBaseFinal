import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { getAnalyticsReport } from '../../../../../core/api/generated/fn/admin-analytics/get-analytics-report';
import { categorySlices } from '../../../charts/category-slices';
import { DonutChart } from '../../../charts/donut-chart';
import { Sparkline } from '../../../charts/sparkline';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { RANGE_SEGMENTS } from '../../../shared/range-segments';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { Badge } from '../../../widgets/badge';
import { Kpi } from '../../../widgets/kpi';
import { RankList } from '../../../widgets/rank-list';
import { SegmentBar } from '../../../widgets/segment-bar';
import { rankRows } from '../rank-rows';

const EMPTY: readonly never[] = [];

/**
 * `/admin/audience`: who the visitors of the period are, in the order of the legacy page.
 * Their figures, their countries (or why there are none: no GeoLite2 database on this
 * instance) and their devices, their browsers, systems and interface languages, then where
 * they came from and the sites that sent them.
 */
@Component({
  selector: 'lodb-audience-panel',
  imports: [
    AdminCard,
    Badge,
    DonutChart,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    RankList,
    SegmentBar,
    Sparkline,
    AdminTextPipe,
  ],
  templateUrl: './audience-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AudiencePanel {
  private readonly texts = injectAdminText();

  protected readonly query = injectQuery();
  protected readonly ranges = RANGE_SEGMENTS;
  protected readonly report = injectPanel(getAnalyticsReport, () => ({
    range: this.query.range(),
  }));
  protected readonly visitors = computed(() =>
    (this.report.value()?.series ?? EMPTY).map((day) => day.visitors),
  );
  protected readonly devices = computed(() =>
    categorySlices('device', rankRows(this.report.value()?.device ?? EMPTY), (name) =>
      this.texts.term('audience.devices', name),
    ),
  );
  protected readonly sources = computed(() =>
    categorySlices('source', rankRows(this.report.value()?.refSource ?? EMPTY), (name) =>
      this.texts.term('audience.sources', name),
    ),
  );
  protected readonly browsers = computed(() => rankRows(this.report.value()?.browser ?? EMPTY));
  protected readonly systems = computed(() => rankRows(this.report.value()?.os ?? EMPTY));
  protected readonly locales = computed(() => rankRows(this.report.value()?.locale ?? EMPTY));
  protected readonly referers = computed(() => rankRows(this.report.value()?.topReferers ?? EMPTY));
  protected readonly countries = computed(() =>
    (this.report.value()?.country ?? EMPTY).map((country) => ({
      name: country.name,
      value: country.count,
    })),
  );
}
