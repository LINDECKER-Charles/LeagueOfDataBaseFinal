import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { readAuditVocabulary } from '../../../../../core/api/generated/fn/admin-audit/read-audit-vocabulary';
import { readUserAuditActivity } from '../../../../../core/api/generated/fn/admin-audit/read-user-audit-activity';
import { Chip } from '../../../../../ui/controls/chip';
import { AdminCard } from '../../../layout/admin-card';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { displayName } from '../../../shared/display-name';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { AuditTable } from '../journal/audit-table';
import { JournalFilters } from '../journal/journal-filters';

/**
 * `/admin/users/:id/activity`: what an account did and what was done to it, from the audit
 * journal, newest first, page by page, filtered by category and by day as the legacy page
 * was, the way back to the accounts first in its toolbar.
 */
@Component({
  selector: 'lodb-user-activity-panel',
  imports: [
    AdminCard,
    AdminPager,
    AuditTable,
    Chip,
    JournalFilters,
    PageHead,
    PanelState,
    RouterLink,
    AdminTextPipe,
  ],
  template: `
    <lodb-page-head
      [eyebrow]="'admin.eyebrows.moderation' | adminText"
      [title]="'admin.activity.title' | adminText: { name: name() }"
      [documentTitle]="'admin.activity.document_title' | adminText: { name: name() }"
      [subtitle]="subtitle()"
    />
    <lodb-journal-filters [vocabulary]="vocabulary.value()" [full]="false">
      <a lodbChip routerLink="/admin/users">{{ 'admin.activity.back' | adminText }}</a>
    </lodb-journal-filters>
    <lodb-panel-state [panel]="activity" />
    @if (activity.value(); as data) {
      <lodb-admin-card>
        <lodb-audit-table
          view="activity"
          empty="admin.activity.empty"
          [entries]="data.activity.items"
          [attr.aria-busy]="activity.busy()"
        />
      </lodb-admin-card>
      <lodb-admin-pager [page]="data.activity.page" [hasMore]="data.activity.hasMore" />
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserActivityPanel {
  private readonly userId = toSignal(
    inject(ActivatedRoute).paramMap.pipe(map((params) => Number(params.get('id')))),
    { requireSync: true },
  );
  private readonly query = injectQuery();
  private readonly texts = injectAdminText();

  protected readonly vocabulary = injectPanel(readAuditVocabulary, () => ({}));
  protected readonly activity = injectPanel(readUserAuditActivity, () => ({
    userId: this.userId(),
    category: this.query.text('category') || undefined,
    from: this.query.text('from') || undefined,
    to: this.query.text('to') || undefined,
    page: this.query.page(),
  }));
  protected readonly name = computed(() => {
    const subject = this.activity.value()?.subject;
    return subject?.username
      ? displayName(subject.username, subject.riotTagline)
      : (this.formerName() ?? `#${this.userId()}`);
  });
  protected readonly subtitle = computed(() => {
    const data = this.activity.value();
    if (data && !data.subject) {
      return this.texts.text('activity.lede_orphan');
    }
    const lede = this.texts.text('activity.lede');
    return data?.subject?.email ? `${data.subject.email} · ${lede}` : lede;
  });

  // A deleted account keeps its journal: it goes by the name its latest action shown bore.
  private formerName(): string | undefined {
    const userId = this.userId();
    const own = this.activity.value()?.activity.items.find((entry) => entry.actorId === userId);
    return own?.actor ?? undefined;
  }
}
