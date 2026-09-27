import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { readAuditJournal } from '../../../../../core/api/generated/fn/admin-audit/read-audit-journal';
import { readAuditVocabulary } from '../../../../../core/api/generated/fn/admin-audit/read-audit-vocabulary';
import { readAuditVolume } from '../../../../../core/api/generated/fn/admin-audit/read-audit-volume';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminCard } from '../../../widgets/admin-card';
import { AdminPager } from '../../../widgets/admin-pager';
import { Kpi } from '../../../widgets/kpi';
import { PageHead } from '../../../widgets/page-head';
import { AuditTable } from './audit-table';
import { JournalFilters } from './journal-filters';
import { JournalPurge } from './journal-purge';

/**
 * `/admin/journal`: the audit journal. What it holds and what the retention keeps, the
 * entries filtered by category, action, outcome, actor and day, newest first, and the purge.
 */
@Component({
  selector: 'lodb-journal-panel',
  imports: [
    AdminCard,
    AdminPager,
    AuditTable,
    FigurePipe,
    JournalFilters,
    JournalPurge,
    Kpi,
    PageHead,
    PanelState,
    StampPipe,
    TranslocoPipe,
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

  protected reload(): void {
    this.volume.reload();
    this.journal.reload();
  }
}
