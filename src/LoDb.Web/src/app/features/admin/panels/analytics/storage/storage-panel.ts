import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { readAdminStorage } from '../../../../../core/api/generated/fn/admin-storage/read-admin-storage';
import type { StorageRow } from '../../../../../core/api/generated/models/storage-row';
import { Button } from '../../../../../ui/controls/button';
import { TimeSeriesChart } from '../../../charts/time-series-chart';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminBand } from '../../../layout/admin-band';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { Kpi } from '../../../widgets/kpi';
import { type RankRow, RankList } from '../../../widgets/rank-list';
import { CoverageMatrix } from './coverage-matrix';
import { LargestObjects } from './largest-objects';

const EMPTY: readonly never[] = [];
const PERCENT = 100;
// The family of the content-addressed images, as the report names it.
const BLOBS = 'blobs';

function byBytes(rows: readonly StorageRow[]): RankRow[] {
  return rows.map((row) => ({ name: row.name, value: row.bytes }));
}

/**
 * `/admin/storage`: what the object storage holds, in the order of the legacy page. Its
 * figures, its families and what it takes in day by day, then its images (extensions, WebP
 * siblings), its data (by version, language and type) and the detail: the heaviest objects
 * and the languages of each version. The API keeps the report a while; "Rafraîchir" recounts
 * it. A storage that could not be read says so in place of the figures.
 */
@Component({
  selector: 'lodb-storage-panel',
  imports: [
    AdminBand,
    AdminCard,
    AdminRule,
    Button,
    CoverageMatrix,
    FigurePipe,
    Kpi,
    LargestObjects,
    PageHead,
    PanelState,
    RankList,
    TimeSeriesChart,
    AdminTextPipe,
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
  protected readonly timeline = computed(() => {
    const days = this.report.value()?.timeline ?? EMPTY;
    const series = [
      {
        label: this.texts.text('storage.series.objects'),
        color: 'var(--color-hex)',
        values: days.map((day) => day.objects),
      },
    ];
    return { dates: days.map((day) => day.date), series };
  });
  protected readonly blobs = computed(
    () => this.report.value()?.families.find((family) => family.name === BLOBS)?.objects ?? 0,
  );
  protected readonly webpWidth = computed(
    () => (this.report.value()?.blobs.webpCoverage ?? 0) * PERCENT,
  );
  protected readonly families = computed(() => byBytes(this.report.value()?.families ?? EMPTY));
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
