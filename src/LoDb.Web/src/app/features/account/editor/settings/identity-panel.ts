import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ProfileService } from '../../../../core/api/generated/services/profile.service';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { Button } from '../../../../ui/controls/button';
import { Field } from '../../../../ui/controls/field';
import { FormSubmission } from '../../shared/forms/form-submission';
import { formText } from '../../shared/forms/form-text';
import { submittedForm } from '../../shared/forms/submitted-form';

/** The summoner name, which is the address of the public card too, and the Riot tag line. */
@Component({
  selector: 'lodb-identity-panel',
  imports: [Button, Field, TranslocoPipe],
  templateUrl: './identity-panel.html',
  styleUrls: ['../../auth/card/auth-form.css', './settings.css'],
  host: { class: 'profile-panel' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IdentityPanel {
  private readonly profiles = inject(ProfileService);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  readonly username = input.required<string>();
  readonly riotTagline = input.required<string | null>();
  /** The identity changed: the editor reads the profile again. */
  readonly saved = output();

  protected readonly submission = new FormSubmission();

  protected async save(event: Event): Promise<void> {
    const form = submittedForm(event);
    const tagline = formText(form, 'riotTagline').trim();
    const body = { username: formText(form, 'username'), riotTagline: tagline || null };
    const call = () => firstValueFrom(this.profiles.setProfileIdentity({ body }));
    if (await this.submission.send(call)) {
      this.toasts.show('success', this.transloco.translate('profile.flash.identity_saved'));
      this.saved.emit();
    }
  }
}
