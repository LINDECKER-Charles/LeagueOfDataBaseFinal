import { ChangeDetectionStrategy, Component, inject, output, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ProfileService } from '../../../../core/api/generated/services/profile.service';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { Button } from '../../../../ui/controls/button';
import { PasswordPair } from '../../password/password-pair';
import { passwordsMatch } from '../../password/password-rules';
import { submittedForm } from '../../shared/forms/submitted-form';
import { FormSubmission } from '../../shared/forms/form-submission';

const MISMATCH = { confirmation: 'auth.register.password_mismatch' };
const REFUSALS = { 'password-exists': 'profile.flash.password_exists' };

/**
 * The first password of an account created through Google, which may then sign in with its
 * e-mail too; the checklist follows the rules the server applies.
 */
@Component({
  selector: 'lodb-password-panel',
  imports: [Button, PasswordPair, TranslocoPipe],
  templateUrl: './password-panel.html',
  styleUrls: ['../../auth/card/auth-form.css', './settings.css'],
  host: { class: 'profile-panel' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PasswordPanel {
  private readonly profiles = inject(ProfileService);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  /** The password is set: the editor reads the profile again. */
  readonly saved = output();

  protected readonly password = signal('');
  protected readonly confirmation = signal('');
  protected readonly submission = new FormSubmission();

  protected async save(event: Event): Promise<void> {
    submittedForm(event);
    if (!passwordsMatch(this.password(), this.confirmation())) {
      this.submission.errors.set(MISMATCH);
      return;
    }
    const body = { password: this.password() };
    const call = () => firstValueFrom(this.profiles.setFirstPassword({ body }));
    if (await this.submission.send(call, REFUSALS)) {
      this.toasts.show('success', this.transloco.translate('profile.flash.password_set'));
      this.saved.emit();
    }
  }
}
