import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { AccountService } from '../../../../core/api/generated/services/account.service';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { ToastService } from '../../../../core/layout/toast/toast-service';
import { Button } from '../../../../ui/controls/button';
import { PasswordPair } from '../../password/password-pair';
import { passwordsMatch } from '../../password/password-rules';
import { submittedForm } from '../../shared/forms/submitted-form';
import type { ApiProblem } from '../../shared/problems/api-problem';
import type { FieldErrors } from '../../shared/problems/field-errors';
import { fieldErrorsOf } from '../../shared/problems/field-errors-of';
import { formErrorOf } from '../../shared/problems/form-error-of';
import { problemOf } from '../../shared/problems/problem-of';
import { AuthCard } from '../card/auth-card';

// The e-mail link: `reset-password/{token}?user={id}`.
const USER_PARAM = 'user';
const USER_ID = /^[1-9][0-9]*$/;
const MISMATCH: FieldErrors = { confirmation: 'auth.register.password_mismatch' };

/**
 * Sets a new password from the link of a reset e-mail, then sends to the login page. A link
 * expired, used or damaged says so and offers to ask for another one.
 */
@Component({
  selector: 'lodb-reset-password-view',
  imports: [AuthCard, Button, PasswordPair, RouterLink, TranslocoPipe],
  templateUrl: './reset-password-view.html',
  styleUrl: '../card/auth-form.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResetPasswordView {
  private readonly account = inject(AccountService);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);
  private readonly page = inject(PageDirection);
  private readonly link = inject(ActivatedRoute).snapshot;
  private readonly token = this.link.paramMap.get('token') ?? '';
  private readonly user = this.link.queryParamMap.get(USER_PARAM) ?? '';

  protected readonly password = signal('');
  protected readonly confirmation = signal('');
  protected readonly busy = signal(false);
  protected readonly invalid = signal(!USER_ID.test(this.user) || this.token === '');
  protected readonly error = signal<string | null>(null);
  protected readonly errors = signal<FieldErrors>({});
  protected readonly forgot = computed(() =>
    localePath(this.page.locale(), 'account/forgot-password'),
  );

  protected reset(event: Event): void {
    submittedForm(event);
    if (passwordsMatch(this.password(), this.confirmation())) {
      void this.send();
    } else {
      this.errors.set(MISMATCH);
    }
  }

  private async send(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    this.errors.set({});
    try {
      const body = { userId: Number(this.user), token: this.token, password: this.password() };
      await firstValueFrom(this.account.resetPassword({ body }));
      this.toasts.show('success', this.transloco.translate('auth.flash.reset_success'));
      await this.router.navigateByUrl(localePath(this.page.locale(), 'account/login'));
    } catch (error) {
      this.refused(problemOf(error));
    } finally {
      this.busy.set(false);
    }
  }

  private refused(problem: ApiProblem): void {
    if (problem.code === 'invalid-token') {
      this.invalid.set(true);
      return;
    }
    this.errors.set(fieldErrorsOf(problem));
    this.error.set(formErrorOf(problem));
  }
}
