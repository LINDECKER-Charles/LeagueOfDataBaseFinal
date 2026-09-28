import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { RegisterRequest } from '../../../../core/api/generated/models/register-request';
import { AccountService } from '../../../../core/api/generated/services/account.service';
import { RETURN_URL_PARAM } from '../../../../core/auth/guards/return-url-param';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { safeReturnUrl } from '../../../../core/routing/safe-return-url';
import { Button } from '../../../../ui/controls/button';
import { Field } from '../../../../ui/controls/field';
import { PasswordPair } from '../../password/password-pair';
import { passwordsMatch } from '../../password/password-rules';
import { formText } from '../../shared/forms/form-text';
import { submittedForm } from '../../shared/forms/submitted-form';
import type { ApiProblem } from '../../shared/problems/api-problem';
import type { FieldErrors } from '../../shared/problems/field-errors';
import { fieldErrorsOf } from '../../shared/problems/field-errors-of';
import { formErrorOf } from '../../shared/problems/form-error-of';
import { problemOf } from '../../shared/problems/problem-of';
import { AuthCard } from '../card/auth-card';
import { GoogleButton } from '../card/google-button';

const MISMATCH: FieldErrors = { confirmation: 'auth.register.password_mismatch' };

/**
 * A new account: e-mail, summoner name, password with its live checklist, and the terms.
 * It is signed in at once and lands where it came from, or on its profile; the e-mail to
 * verify the address is on its way.
 */
@Component({
  selector: 'lodb-register-view',
  imports: [AuthCard, Button, Field, GoogleButton, PasswordPair, RouterLink, TranslocoPipe],
  templateUrl: './register-view.html',
  styleUrl: '../card/auth-form.css',
  // Grows with the account page, so its card is centred between header and footer.
  host: { class: 'flex flex-1 flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterView {
  private readonly account = inject(AccountService);
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly origin = inject(DOCUMENT).location.origin;
  private readonly page = inject(PageDirection);
  private readonly returnUrl = inject(ActivatedRoute).snapshot.queryParamMap.get(RETURN_URL_PARAM);

  protected readonly password = signal('');
  protected readonly confirmation = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errors = signal<FieldErrors>({});
  protected readonly link = (path: string) => localePath(this.page.locale(), path);
  protected readonly loginQuery = this.returnUrl === null ? {} : { returnUrl: this.returnUrl };

  protected register(event: Event): void {
    const form = submittedForm(event);
    if (!passwordsMatch(this.password(), this.confirmation())) {
      this.errors.set(MISMATCH);
      return;
    }
    void this.send({
      email: formText(form, 'email'),
      username: formText(form, 'username'),
      password: this.password(),
      acceptTerms: form.has('acceptTerms'),
      locale: this.page.locale(),
    });
  }

  private async send(body: RegisterRequest): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.errors.set({});
    try {
      this.session.apply(await firstValueFrom(this.account.registerAccount({ body })));
      this.toasts.show('success', this.transloco.translate('auth.flash.registered'));
      this.toasts.show('info', this.transloco.translate('auth.flash.verify_sent'));
      const profile = this.link('account/profile');
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl, this.origin, profile));
    } catch (error) {
      this.refused(problemOf(error));
    } finally {
      this.busy.set(false);
    }
  }

  private refused(problem: ApiProblem): void {
    this.errors.set(fieldErrorsOf(problem));
    this.error.set(formErrorOf(problem));
  }
}
