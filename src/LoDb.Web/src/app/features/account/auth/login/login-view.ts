import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  afterNextRender,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { LoginRequest } from '../../../../core/api/generated/models/login-request';
import { RETURN_URL_PARAM } from '../../../../core/auth/guards/return-url-param';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { Button } from '../../../../ui/controls/button';
import { Field } from '../../../../ui/controls/field';
import { formText } from '../../shared/forms/form-text';
import { submittedForm } from '../../shared/forms/submitted-form';
import { problemOf } from '../../shared/problems/problem-of';
import { AuthCard } from '../card/auth-card';
import { GoogleButton } from '../card/google-button';
import { loginErrorKey } from './login-error-key';

// The callback of a failed Google sign-in names its reason in this parameter.
const GOOGLE_ERROR_PARAM = 'error';
const SPACES = /\s+/g;

type Step = 'credentials' | 'two-factor';

/**
 * Sign-in with an e-mail or a summoner name and a password, then, for an account with an
 * authenticator, a second step asking its code (or a recovery code) with the same password.
 * A success lands on the page the visitor came from when it is safe, the profile otherwise.
 * The identifier has the focus on arrival, as on the legacy page.
 */
@Component({
  selector: 'lodb-login-view',
  imports: [AuthCard, Button, Field, GoogleButton, RouterLink, TranslocoPipe],
  templateUrl: './login-view.html',
  styleUrl: '../card/auth-form.css',
  // Grows with the account page, so its card is centred between header and footer.
  host: { class: 'flex flex-1 flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginView {
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);
  private readonly query = inject(ActivatedRoute).snapshot.queryParamMap;
  private readonly page = inject(PageDirection);
  private credentials: LoginRequest = { identifier: null, password: null };

  protected readonly step = signal<Step>('credentials');
  protected readonly useRecovery = signal(false);
  protected readonly remember = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(this.googleError());
  protected readonly link = (path: string) => localePath(this.page.locale(), path);
  protected readonly registerQuery = this.returnQuery();
  private readonly identifier = viewChild<ElementRef<HTMLInputElement>>('identifier');

  constructor() {
    // Programmatic: the view renders after the first paint, past the browser's autofocus.
    afterNextRender(() => this.identifier()?.nativeElement.focus());
  }

  protected signIn(event: Event): void {
    const form = submittedForm(event);
    this.credentials = {
      identifier: formText(form, 'identifier'),
      password: formText(form, 'password'),
      rememberMe: this.remember(),
    };
    void this.attempt(this.credentials);
  }

  protected confirm(event: Event): void {
    const form = submittedForm(event);
    const field = this.useRecovery() ? 'recoveryCode' : 'twoFactorCode';
    void this.attempt({ ...this.credentials, [field]: formText(form, field).replace(SPACES, '') });
  }

  protected toggleRecovery(): void {
    this.useRecovery.update((recovery) => !recovery);
    this.error.set(null);
  }

  protected rememberFrom(event: Event): void {
    this.remember.set((event.target as HTMLInputElement).checked);
  }

  private async attempt(request: LoginRequest): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const landing = await this.session.signIn(request, {
        returnUrl: this.query.get(RETURN_URL_PARAM),
        fallbackUrl: this.link('account/profile'),
      });
      await this.router.navigateByUrl(landing);
    } catch (error) {
      this.refused(problemOf(error).code);
    } finally {
      this.busy.set(false);
    }
  }

  private refused(code: string | null): void {
    if (code === 'two-factor-required') {
      this.step.set('two-factor');
    } else {
      this.error.set(loginErrorKey(code));
    }
  }

  private googleError(): string | null {
    const code = this.query.get(GOOGLE_ERROR_PARAM);
    return code === null ? null : loginErrorKey(code);
  }

  // The registration page brings the visitor back to the same page.
  private returnQuery(): Record<string, string> {
    const returnUrl = this.query.get(RETURN_URL_PARAM);
    return returnUrl === null ? {} : { [RETURN_URL_PARAM]: returnUrl };
  }
}
