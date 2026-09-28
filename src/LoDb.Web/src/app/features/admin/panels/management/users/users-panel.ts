import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { banAdminUser } from '../../../../../core/api/generated/fn/admin-users/ban-admin-user';
import { deleteAdminUser } from '../../../../../core/api/generated/fn/admin-users/delete-admin-user';
import { searchAdminUsers } from '../../../../../core/api/generated/fn/admin-users/search-admin-users';
import { unbanAdminUser } from '../../../../../core/api/generated/fn/admin-users/unban-admin-user';
import type { AdminUserRow } from '../../../../../core/api/generated/models/admin-user-row';
import { AuthSession } from '../../../../../core/auth/session/auth-session';
import { Button } from '../../../../../ui/controls/button';
import { Chip } from '../../../../../ui/controls/chip';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminRule } from '../../../layout/admin-rule';
import { PageHead } from '../../../layout/page-head';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { displayName } from '../../../shared/display-name';
import { formText } from '../../../shared/form-text';
import { injectRowActions } from '../../../shared/http/inject-row-actions';
import { injectQuery } from '../../../shared/inject-query';
import { injectPanel } from '../../../state/inject-panel';
import { PanelState } from '../../../state/panel-state';
import { AdminPager } from '../../../widgets/admin-pager';
import { Kpi } from '../../../widgets/kpi';
import { UserActions } from './user-actions';
import { UserBadges } from './user-badges';

/**
 * `/admin/users`: the accounts, searched by name or e-mail, in the legacy table. An
 * administrator bans one (with a reason the account is told), lifts a ban, deletes an account
 * with its builds, or opens its activity in the journal. Their own account offers its
 * activity only.
 */
@Component({
  selector: 'lodb-users-panel',
  imports: [
    AdminCard,
    AdminPager,
    AdminRule,
    Button,
    Chip,
    FigurePipe,
    Kpi,
    PageHead,
    PanelState,
    RouterLink,
    StampPipe,
    UserActions,
    UserBadges,
    AdminTextPipe,
  ],
  templateUrl: './users-panel.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersPanel {
  private readonly session = inject(AuthSession);

  protected readonly query = injectQuery();
  protected readonly users = injectPanel(searchAdminUsers, () => ({
    q: this.query.text('q') || undefined,
    page: this.query.page(),
  }));
  protected readonly actions = injectRowActions(() => this.users.reload());
  /** The signed-in administrator, whose row offers no moderation. */
  protected readonly selfId = computed(() => this.session.user()?.id ?? null);

  protected nameOf(user: AdminUserRow): string {
    return displayName(user.username, user.riotTagline);
  }

  protected search(event: Event): void {
    this.query.set({ q: formText(event, 'q').trim() || null });
  }

  protected ban(user: AdminUserRow, reason: string): void {
    void this.actions.run(user.id, {
      call: banAdminUser,
      params: { id: user.id, body: { reason: reason || null } },
      done: { key: 'users.done.ban', params: { name: user.username } },
    });
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
