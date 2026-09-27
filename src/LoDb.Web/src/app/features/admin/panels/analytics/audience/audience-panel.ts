import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { getAnalyticsReport } from '../../../../../core/api/generated/fn/admin-analytics/get-analytics-report';
import type { AnalyticsReport } from '../../../../../core/api/generated/models/analytics-report';
import { DonutChart } from '../../../charts/donut-chart';
import { paletteSlices } from '../../../charts/palette-slices';
import { FigurePipe } from '../../../format/figure-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminCard } from '../../../widgets/admin-card';
import { Kpi } from '../../../widgets/kpi';
import { PageHead } from '../../../widgets/page-head';
import { RangeBar } from '../../../widgets/range-bar';
import { RankList } from '../../../widgets/rank-list';
import { rankRows } from '../rank-rows';

// The rankings a long report folds after this many rows.
const RANK_LIMIT = 8;
const EMPTY: readonly never[] = [];

/**
 * `/admin/audience`: who the visitors of the period are. Their devices and where they come
 * from as rings, their browsers, systems, countries and languages ranked, and the sites that
 * sent them.
 */
@Component({
  selector: 'lodb-audience-panel',
  imports: [
    AdminCard,
    DonutChart,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    RangeBar,
    RankList,
    TranslocoPipe,
  ],
  templateUrl: './audience-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AudiencePanel {
  private readonly texts = injectAdminText();

  protected readonly query = injectQuery();
  protected readonly report = injectPanel(getAnalyticsReport, () => ({
    range: this.query.range(),
  }));
  protected readonly limit = RANK_LIMIT;
  protected readonly devices = computed(() => this.slices('device', 'audience.devices'));
  protected readonly sources = computed(() => this.slices('refSource', 'audience.sources'));
  protected readonly browsers = computed(() => rankRows(this.report.value()?.browser ?? EMPTY));
  protected readonly systems = computed(() => rankRows(this.report.value()?.os ?? EMPTY));
  protected readonly languages = computed(() => rankRows(this.report.value()?.lang ?? EMPTY));
  protected readonly locales = computed(() => rankRows(this.report.value()?.locale ?? EMPTY));
  protected readonly referers = computed(() => rankRows(this.report.value()?.topReferers ?? EMPTY));
  protected readonly countries = computed(() =>
    (this.report.value()?.country ?? EMPTY).map((country) => ({
      name: `${country.name} (${country.code})`,
      value: country.count,
    })),
  );

  private slices(field: 'device' | 'refSource', terms: string) {
    const ranks: AnalyticsReport[typeof field] = this.report.value()?.[field] ?? [];
    return paletteSlices(
      rankRows(ranks, (name) => this.texts.term(terms, name)),
      this.texts.text('common.others'),
    );
  }
}
