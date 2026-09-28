import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { DeletionConfirmation } from '../../../../core/api/generated/models/deletion-confirmation';
import { ProfileService } from '../../../../core/api/generated/services/profile.service';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { Field } from '../../../../ui/controls/field';
import { FormSubmission } from '../../shared/forms/form-submission';
import { formText } from '../../shared/forms/form-text';
import { submittedForm } from '../../shared/forms/submitted-form';

/**
 * The erasure of the account, confirmed as the server asks: the phrase of the page's locale
 * for a Google account, the password for another one that has one, nothing otherwise. The
 * account then leaves for the home page, signed out.
 */
@Component({
  selector: 'lodb-danger-panel',
  imports: [Field, TranslocoPipe],
  templateUrl: './danger-panel.html',
  styleUrls: ['../../auth/card/auth-form.css', './settings.css'],
  host: { class: 'profile-danger' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DangerPanel {
  private readonly profiles = inject(ProfileService);
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);
  private readonly page = inject(PageDirection);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  readonly confirmation = input.required<DeletionConfirmation>();

  protected readonly help = computed(() =>
    this.confirmation() === 'password' ? 'profile.danger.help' : 'profile.danger.help_no_password',
  );
  protected readonly submission = new FormSubmission();

  protected async erase(event: Event): Promise<void> {
    const form = submittedForm(event);
    const locale = this.page.locale();
    const body = {
      locale,
      password: form.has('password') ? formText(form, 'password') : null,
      confirmation: form.has('confirmation') ? formText(form, 'confirmation') : null,
    };
    const call = () => firstValueFrom(this.profiles.deleteOwnAccount({ body }));
    if (await this.submission.send(call)) {
      // The account is gone, and its session with it.
      this.session.apply({ user: null });
      this.toasts.show('success', this.transloco.translate('profile.flash.deleted'));
      await this.router.navigateByUrl(localePath(locale, ''));
    }
  }
}
