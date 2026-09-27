import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { readAdminStorage } from '../../../../../core/api/generated/fn/admin-storage/read-admin-storage';
import type { StorageRow } from '../../../../../core/api/generated/models/storage-row';
import { Button } from '../../../../../ui/controls/button';
import { DonutChart } from '../../../charts/donut-chart';
import { paletteSlices } from '../../../charts/palette-slices';
import { TimeSeriesChart } from '../../../charts/time-series-chart';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminCard } from '../../../widgets/admin-card';
import { Kpi } from '../../../widgets/kpi';
import { Legend } from '../../../widgets/legend';
import { PageHead } from '../../../widgets/page-head';
import { type RankRow, RankList } from '../../../widgets/rank-list';

const RANK_LIMIT = 8;
const EMPTY: readonly never[] = [];

function byBytes(rows: readonly StorageRow[]): RankRow[] {
  return rows.map((row) => ({ name: row.name, value: row.bytes }));
}

/**
 * `/admin/storage`: what the object storage holds. Its growth day by day, its families,
 * extensions and datasets by weight, the largest objects, how much the deduplication and
 * the WebP siblings save. The API keeps the report a while; "Refresh" recounts it.
 */
@Component({
  selector: 'lodb-storage-panel',
  imports: [
    AdminCard,
    Button,
    DonutChart,
    FigurePipe,
    Kpi,
    Legend,
    PageHead,
    PanelState,
    RankList,
    StampPipe,
    TimeSeriesChart,
    TranslocoPipe,
  ],
  templateUrl: './storage-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StoragePanel {
  private readonly texts = injectAdminText();
  private readonly refreshes = signal(0);

  protected readonly report = injectPanel(readAdminStorage, () =>
    this.refreshes() > 0 ? { refresh: true } : {},
  );
  protected readonly limit = RANK_LIMIT;
  protected readonly timeline = computed(() => {
    const days = this.report.value()?.timeline ?? EMPTY;
    const series = [
      {
        label: this.texts.text('storage.series.total'),
        color: 'var(--color-gold)',
        values: days.map((day) => day.cumulativeBytes),
        format: 'bytes' as const,
      },
      {
        label: this.texts.text('storage.series.added'),
        color: 'var(--color-hex)',
        values: days.map((day) => day.bytes),
        format: 'bytes' as const,
      },
    ];
    return { dates: days.map((day) => day.date), series };
  });
  protected readonly families = computed(() =>
    paletteSlices(
      byBytes(this.report.value()?.families ?? EMPTY),
      this.texts.text('common.others'),
    ),
  );
  protected readonly extensions = computed(() =>
    byBytes(this.report.value()?.blobs.byExt ?? EMPTY),
  );
  protected readonly types = computed(() => byBytes(this.report.value()?.data.byType ?? EMPTY));
  protected readonly languages = computed(() => byBytes(this.report.value()?.data.byLang ?? EMPTY));
  protected readonly versions = computed(() =>
    byBytes(this.report.value()?.data.byVersion ?? EMPTY),
  );

  protected refresh(): void {
    this.refreshes.update((count) => count + 1);
  }
}
