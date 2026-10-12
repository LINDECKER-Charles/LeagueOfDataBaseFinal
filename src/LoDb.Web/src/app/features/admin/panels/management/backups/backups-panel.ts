import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { readAdminBackups } from '../../../../../core/api/generated/fn/admin-backups/read-admin-backups';
import type { BackupRepositoryState } from '../../../../../core/api/generated/models/backup-repository-state';
import { Sparkline } from '../../../charts/sparkline';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminBand } from '../../../layout/admin-band';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { Kpi } from '../../../widgets/kpi';
import { SegmentBar, type Segment } from '../../../widgets/segment-bar';
import { BackupDatabaseCard } from './backup-database-card';
import { backupNights } from './backup-nights';
import { BackupNightsStrip } from './backup-nights-strip';
import { BackupRunsTable } from './backup-runs-table';
import { monthlyCost } from './monthly-cost';
import { unitPrice } from './unit-price';

// The windows of the history, in days, read as the analytics periods: "30 j", "30 jours".
const WINDOWS = ['7', '30', '90'] as const;
const DEFAULT_WINDOW = '30';
const DAYS = 'days';
const WINDOW_SEGMENTS: readonly Segment[] = WINDOWS.map((days) => ({
  value: days,
  label: `admin.range.${days}d`,
  name: `admin.range_long.${days}d`,
}));
const OK = 'ok';
const RETENTION = 'retention';

/**
 * `/admin/backups`: whether the nightly off-host backups of the host went through. The key
 * figures (the databases up to date, the last success, the size of the repository and what it
 * is estimated to cost a month), a card per database, a square per night of the window, then
 * every step the backup container reported, newest first. Read only: infra-vps runs them.
 */
@Component({
  selector: 'lodb-backups-panel',
  imports: [
    AdminBand,
    AdminCard,
    AdminRule,
    BackupDatabaseCard,
    BackupNightsStrip,
    BackupRunsTable,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    SegmentBar,
    Sparkline,
    StampPipe,
    AdminTextPipe,
  ],
  templateUrl: './backups-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BackupsPanel {
  protected readonly query = injectQuery();
  protected readonly windows = WINDOW_SEGMENTS;
  /** The window in the URL, 30 days unless one of the others. */
  protected readonly days = computed(() => {
    const days = this.query.text(DAYS);
    return (WINDOWS as readonly string[]).includes(days) ? days : DEFAULT_WINDOW;
  });
  protected readonly report = injectPanel(readAdminBackups, () => ({ days: Number(this.days()) }));
  protected readonly nights = computed(() => {
    const report = this.report.value();
    return report ? backupNights(report.runs, report.days, report.generatedAt) : [];
  });
  protected readonly upToDate = computed(
    () =>
      (this.report.value()?.databases ?? []).filter((database) => database.status === OK).length,
  );
  /** The end of the latest backup that succeeded, of any database. */
  protected readonly lastSuccess = computed(() =>
    (this.report.value()?.databases ?? [])
      .map((database) => database.lastSuccessAt ?? '')
      .reduce((latest, at) => (at > latest ? at : latest), ''),
  );
  /** The size of the repository at each retention of the window, oldest first. */
  protected readonly sizes = computed(() =>
    (this.report.value()?.runs ?? [])
      .filter((run) => run.step === RETENTION && run.repositoryBytes !== null)
      .map((run) => run.repositoryBytes ?? 0)
      .reverse(),
  );

  protected cost(repository: BackupRepositoryState): string {
    return monthlyCost(repository.monthlyCost, repository.currency);
  }

  protected price(repository: BackupRepositoryState): string {
    return unitPrice(repository.pricePerGbMonth, repository.currency);
  }
}
