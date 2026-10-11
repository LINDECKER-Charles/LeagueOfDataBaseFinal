import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { Button } from '../../../../ui/controls/button';
import { accountMessage } from '../../shared/account-message';
import { FormSubmission } from '../../shared/forms/form-submission';

/**
 * The sessions of the account: one button closes them all, on every browser and in every
 * app, this one included, for a device lost or shared. The account then lands on the sign-in
 * page.
 */
@Component({
  selector: 'lodb-sessions-panel',
  imports: [Button, TranslocoPipe],
  templateUrl: './sessions-panel.html',
  styleUrls: ['../../auth/card/auth-form.css', './settings.css'],
  host: { class: 'profile-panel' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionsPanel {
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);
  private readonly page = inject(PageDirection);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  protected readonly submission = new FormSubmission();

  protected async signOutEverywhere(): Promise<void> {
    const locale = this.page.locale();
    if (await this.submission.send(() => this.session.signOutEverywhere())) {
      this.toasts.show(
        'success',
        await accountMessage(this.transloco, locale, 'account.sessions.signed_out'),
      );
      await this.router.navigateByUrl(localePath(locale, 'account/login'));
    }
  }
}
