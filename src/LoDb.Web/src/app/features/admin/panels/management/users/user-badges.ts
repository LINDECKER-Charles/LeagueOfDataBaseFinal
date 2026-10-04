import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { AdminUserRow } from '../../../../../core/api/generated/models/admin-user-row';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { Badge } from '../../../widgets/badge';

/**
 * The statuses of an account, the legacy badges in their order (banned, supporter, signed in
 * with Google, public profile), then what the new admin also knows: an administrator, an
 * address never verified. A dash when none applies.
 */
@Component({
  selector: 'lodb-user-badges',
  imports: [Badge, AdminTextPipe],
  template: `
    @let account = user();
    @if (hasAny()) {
      <span class="badges">
        @if (account.isBanned) {
          <span lodbBadge="bad">{{ 'admin.users.badges.banned' | adminText }}</span>
        }
        @if (account.isSupporter) {
          <span lodbBadge="good">{{ 'admin.users.badges.supporter' | adminText }}</span>
        }
        @if (account.google) {
          <span lodbBadge>{{ 'admin.users.badges.google' | adminText }}</span>
        }
        @if (account.isPublicProfile) {
          <span lodbBadge="warn">{{ 'admin.users.badges.public_profile' | adminText }}</span>
        }
        @if (account.isAdmin) {
          <span lodbBadge>{{ 'admin.users.badges.admin' | adminText }}</span>
        }
        @if (!account.emailVerified) {
          <span lodbBadge="warn">{{ 'admin.users.badges.unverified' | adminText }}</span>
        }
      </span>
    } @else {
      <span class="text-text-dim">—</span>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserBadges {
  readonly user = input.required<AdminUserRow>();

  protected hasAny(): boolean {
    const user = this.user();
    return (
      user.isBanned ||
      user.isSupporter ||
      user.google ||
      user.isPublicProfile ||
      user.isAdmin ||
      !user.emailVerified
    );
  }
}
