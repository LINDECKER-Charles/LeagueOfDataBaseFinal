import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { AdminUserRow } from '../../../../../core/api/generated/models/admin-user-row';
import { Button } from '../../../../../ui/controls/button';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { formText } from '../../../shared/form-text';
import { ConfirmButton } from '../../../widgets/confirm-button';

/** The longest reason of a ban the API keeps. */
const REASON_LENGTH = 255;

/**
 * The actions of an account's row, as the legacy row offered them: its activity; lifting a
 * ban, or banning it with an optional reason typed in the row; deleting it for good, once
 * confirmed. The signed-in administrator's own row offers its activity only: the API refuses
 * to moderate one's own account.
 */
@Component({
  selector: 'lodb-user-actions',
  imports: [Button, ConfirmButton, RouterLink, AdminTextPipe],
  template: `
    @let account = user();
    <a
      lodbButton="ghost"
      lodbButtonSize="small"
      [routerLink]="['/admin/users', account.id, 'activity']"
    >
      {{ 'admin.users.activity' | adminText }}
    </a>
    @if (self()) {
      <span class="text-text-dim">—</span>
    } @else {
      @if (account.isBanned) {
        <button
          lodbButton="ghost"
          lodbButtonSize="small"
          type="button"
          [disabled]="busy()"
          (click)="unbanned.emit()"
        >
          {{ 'admin.users.unban' | adminText }}
        </button>
      } @else {
        <form class="row-form" (submit)="ban($event)">
          <input
            name="reason"
            type="text"
            class="admin-input"
            [attr.maxlength]="reasonLength"
            [placeholder]="'admin.users.reason_placeholder' | adminText"
            [attr.aria-label]="'admin.users.reason_label' | adminText"
          />
          <button lodbButton lodbButtonSize="small" type="submit" [disabled]="busy()">
            {{ 'admin.users.ban' | adminText }}
          </button>
        </form>
      }
      <lodb-confirm-button
        tone="danger"
        confirmTone="danger"
        [label]="'admin.actions.delete' | adminText"
        [confirmLabel]="'admin.users.confirm_delete' | adminText"
        [busy]="busy()"
        (confirmed)="deleted.emit()"
      />
    }
  `,
  host: { class: 'inline-flex flex-nowrap items-center justify-end gap-[0.45rem]' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserActions {
  readonly user = input.required<AdminUserRow>();
  /** Whether the row is the signed-in administrator's own account. */
  readonly self = input(false);
  readonly busy = input(false);
  /** Asks for a ban, with its reason ('' for none). */
  readonly banned = output<string>();
  readonly unbanned = output<void>();
  readonly deleted = output<void>();

  protected readonly reasonLength = REASON_LENGTH;

  protected ban(event: Event): void {
    this.banned.emit(formText(event, 'reason').trim());
  }
}
