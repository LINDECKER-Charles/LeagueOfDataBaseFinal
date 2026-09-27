import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { readUserAuditActivity } from '../../../../../core/api/generated/fn/admin-audit/read-user-audit-activity';
import { Button } from '../../../../../ui/controls/button';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { PageHead } from '../../../widgets/page-head';
import { AuditTable } from '../journal/audit-table';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';

/**
 * `/admin/users/:id/activity`: what an account did and what was done to it, from the audit
 * journal, newest first, page by page.
 */
@Component({
  selector: 'lodb-user-activity-panel',
  imports: [AdminPager, AuditTable, Button, PageHead, PanelState, RouterLink, AdminTextPipe],
  template: `
    <lodb-page-head
      [eyebrow]="'admin.nav.users' | adminText"
      [title]="'admin.activity.title' | adminText: { name: name() }"
      [subtitle]="activity.value()?.subject?.email ?? ''"
    >
      <a lodbButton="ghost" routerLink="/admin/users">{{ 'admin.activity.back' | adminText }}</a>
    </lodb-page-head>
    <lodb-panel-state [panel]="activity" />
    @if (activity.value(); as data) {
      <lodb-audit-table [entries]="data.activity.items" [attr.aria-busy]="activity.busy()" />
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

  protected readonly query = injectQuery();
  protected readonly activity = injectPanel(readUserAuditActivity, () => ({
    userId: this.userId(),
    page: this.query.page(),
  }));
  protected readonly name = computed(() => {
    const subject = this.activity.value()?.subject;
    return subject?.username ?? `#${this.userId()}`;
  });
}
