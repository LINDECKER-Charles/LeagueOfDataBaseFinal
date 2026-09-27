import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { banAdminUser } from '../../../../../core/api/generated/fn/admin-users/ban-admin-user';
import { deleteAdminUser } from '../../../../../core/api/generated/fn/admin-users/delete-admin-user';
import { searchAdminUsers } from '../../../../../core/api/generated/fn/admin-users/search-admin-users';
import { unbanAdminUser } from '../../../../../core/api/generated/fn/admin-users/unban-admin-user';
import type { AdminUserRow } from '../../../../../core/api/generated/models/admin-user-row';
import { Button } from '../../../../../ui/controls/button';
import { Field } from '../../../../../ui/controls/field';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { formText } from '../../../shared/form-text';
import { injectRowActions } from '../../../shared/http/inject-row-actions';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { Badge } from '../../../widgets/badge';
import { ConfirmButton } from '../../../widgets/confirm-button';
import { Kpi } from '../../../widgets/kpi';
import { PageHead } from '../../../widgets/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';

/**
 * `/admin/users`: the accounts, searched by name or e-mail. An administrator bans one (with
 * a reason the account is told), lifts a ban, deletes an account with its builds, or opens
 * its activity in the journal. The API refuses an administrator's action on their own
 * account.
 */
@Component({
  selector: 'lodb-users-panel',
  imports: [
    AdminPager,
    Badge,
    Button,
    ConfirmButton,
    Field,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    RouterLink,
    StampPipe,
    AdminTextPipe,
  ],
  templateUrl: './users-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPanel {
  protected readonly query = injectQuery();
  protected readonly users = injectPanel(searchAdminUsers, () => ({
    q: this.query.text('q') || undefined,
    page: this.query.page(),
  }));
  protected readonly actions = injectRowActions(() => this.users.reload());
  /** The account whose ban form is open. */
  protected readonly banning = signal<number | null>(null);

  protected search(event: Event): void {
    this.query.set({ q: formText(event, 'q').trim() || null });
  }

  protected async ban(event: Event, user: AdminUserRow): Promise<void> {
    const reason = formText(event, 'reason').trim();
    const banned = await this.actions.run(user.id, {
      call: banAdminUser,
      params: { id: user.id, body: { reason: reason || null } },
      done: { key: 'users.done.ban', params: { name: user.username } },
    });
    if (banned !== null) {
      this.banning.set(null);
    }
  }

  protected unban(user: AdminUserRow): void {
    void this.actions.run(user.id, {
      call: unbanAdminUser,
      params: { id: user.id },
      done: { key: 'users.done.unban', params: { name: user.username } },
    });
  }

  protected remove(user: AdminUserRow): void {
    void this.actions.run(user.id, {
      call: deleteAdminUser,
      params: { id: user.id },
      done: { key: 'users.done.delete', params: { name: user.username } },
    });
  }
}
