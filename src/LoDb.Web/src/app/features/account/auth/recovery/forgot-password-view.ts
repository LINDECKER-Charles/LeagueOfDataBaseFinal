import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { AccountService } from '../../../../core/api/generated/services/account.service';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { Button } from '../../../../ui/controls/button';
import { Field } from '../../../../ui/controls/field';
import { formText } from '../../shared/forms/form-text';
import { submittedForm } from '../../shared/forms/submitted-form';
import { overrideAccountTitle } from '../../shared/head/override-account-title';
import type { FieldErrors } from '../../shared/problems/field-errors';
import { fieldErrorsOf } from '../../shared/problems/field-errors-of';
import { formErrorOf } from '../../shared/problems/form-error-of';
import { problemOf } from '../../shared/problems/problem-of';
import { AuthCard } from '../card/auth-card';

/**
 * Asks for a reset link. The answer never tells whether the address has an account, so the
 * page says the same whatever it is: check the inbox. The address field has the focus on
 * arrival, as on the legacy page.
 */
@Component({
  selector: 'lodb-forgot-password-view',
  imports: [AuthCard, Button, Field, RouterLink, TranslocoPipe],
  templateUrl: './forgot-password-view.html',
  styleUrl: '../card/auth-form.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForgotPasswordView {
  private readonly account = inject(AccountService);
  private readonly page = inject(PageDirection);

  protected readonly sent = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly errors = signal<FieldErrors>({});
  protected readonly heading = computed(() =>
    this.sent() ? 'auth.reset.check_email_title' : 'auth.reset.request_title',
  );
  protected readonly login = computed(() => localePath(this.page.locale(), 'account/login'));
  private readonly email = viewChild<ElementRef<HTMLInputElement>>('email');

  constructor() {
    afterNextRender(() => this.email()?.nativeElement.focus());
    // The legacy "check your inbox" was a page of its own, titled so.
    overrideAccountTitle(() => (this.sent() ? { key: 'auth.reset.check_email_title' } : null));
  }

  protected async request(event: Event): Promise<void> {
    const email = formText(submittedForm(event), 'email');
    this.busy.set(true);
    this.error.set(null);
    this.errors.set({});
    try {
      const body = { email, locale: this.page.locale() };
      await firstValueFrom(this.account.requestPasswordReset({ body }));
      this.sent.set(true);
    } catch (error) {
      const problem = problemOf(error);
      this.errors.set(fieldErrorsOf(problem));
      this.error.set(formErrorOf(problem));
    } finally {
      this.busy.set(false);
    }
  }
}
