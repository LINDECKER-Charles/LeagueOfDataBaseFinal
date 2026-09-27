import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { readAuditJournal } from '../../../../../core/api/generated/fn/admin-audit/read-audit-journal';
import { readAuditVocabulary } from '../../../../../core/api/generated/fn/admin-audit/read-audit-vocabulary';
import { readAuditVolume } from '../../../../../core/api/generated/fn/admin-audit/read-audit-volume';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { Kpi } from '../../../widgets/kpi';
import { AuditTable } from './audit-table';
import { JournalFilters } from './journal-filters';
import { JournalPurge } from './journal-purge';

// The legal retention (CNIL), which the lede states before the API told its own.
const RETENTION_MONTHS = 6;

/**
 * `/admin/journal`: the audit journal, in the order of the legacy page. What it holds and
 * what the retention keeps, the entries filtered by category, day, outcome, actor and action,
 * newest first, and the manual purge.
 */
@Component({
  selector: 'lodb-journal-panel',
  imports: [
    AdminCard,
    AdminPager,
    AdminRule,
    AuditTable,
    FigurePipe,
    JournalFilters,
    JournalPurge,
    Kpi,
    PageHead,
    PanelState,
    StampPipe,
    AdminTextPipe,
  ],
  templateUrl: './journal-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JournalPanel {
  protected readonly query = injectQuery();
  protected readonly volume = injectPanel(readAuditVolume, () => ({}));
  protected readonly vocabulary = injectPanel(readAuditVocabulary, () => ({}));
  protected readonly journal = injectPanel(readAuditJournal, () => {
    const action = this.query.text('action');
    const text = (name: string) => this.query.text(name) || undefined;
    return {
      action: action ? [action] : undefined,
      category: text('category'),
      outcome: text('outcome'),
      actorType: text('actorType'),
      actor: text('actor'),
      from: text('from'),
      to: text('to'),
      page: this.query.page(),
    };
  });
  protected readonly months = computed(
    () => this.volume.value()?.retentionMonths ?? RETENTION_MONTHS,
  );

  protected reload(): void {
    this.volume.reload();
    this.journal.reload();
  }
}
