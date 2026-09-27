import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { LoginRequest } from '../../../core/api/generated/models/login-request';
import { RETURN_URL_PARAM } from '../../../core/auth/guards/return-url-param';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { Button } from '../../../ui/controls/button';
import { Field } from '../../../ui/controls/field';
import { Frame } from '../../../ui/surfaces/frame';
import { adminAccessOf } from '../access/admin-access';
import { ADMIN_PATHS } from '../shared/admin-paths';
import { formText } from '../shared/form-text';
import { problemOf } from '../shared/http/problem-of';
import { PageHead } from '../widgets/page-head';
import { adminLoginErrorKey } from './admin-login-error-key';

const SPACES = /\s+/g;

type Step = 'credentials' | 'second-factor';

/**
 * The door of the admin, `/admin/login`: the password, then the code of the authenticator
 * (or a recovery code) for an administrator who enrolled one. The session stays until the
 * browser closes. An administrator without an authenticator goes on to enrol one; an
 * account that is no administrator is told the admin is not for it.
 */
@Component({
  selector: 'lodb-admin-login-page',
  imports: [Button, Field, Frame, PageHead, TranslocoPipe],
  templateUrl: './admin-login-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminLoginPage {
  private readonly session = inject(AuthSession);
  private readonly router = inject(Router);
  private readonly returnUrl = inject(ActivatedRoute).snapshot.queryParamMap.get(RETURN_URL_PARAM);
  private credentials: LoginRequest = { identifier: null, password: null };

  protected readonly step = signal<Step>('credentials');
  protected readonly useRecovery = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected signIn(event: Event): void {
    this.credentials = {
      identifier: formText(event, 'identifier').trim(),
      password: formText(event, 'password'),
      rememberMe: false,
    };
    void this.attempt(this.credentials);
  }

  protected confirm(event: Event): void {
    const field = this.useRecovery() ? 'recoveryCode' : 'twoFactorCode';
    void this.attempt({ ...this.credentials, [field]: formText(event, field).replace(SPACES, '') });
  }

  protected toggleRecovery(): void {
    this.useRecovery.update((recovery) => !recovery);
    this.error.set(null);
  }

  private async attempt(request: LoginRequest): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const landing = await this.session.signIn(request, {
        returnUrl: this.returnUrl,
        fallbackUrl: ADMIN_PATHS.home,
      });
      await this.land(landing);
    } catch (error) {
      this.refused(problemOf(error).code);
    } finally {
      this.busy.set(false);
    }
  }

  private async land(landing: string): Promise<void> {
    const access = adminAccessOf(this.session.user());
    if (access === 'refused') {
      this.error.set('login.errors.refused');
      return;
    }
    await this.router.navigateByUrl(access === 'enroll' ? ADMIN_PATHS.enroll : landing);
  }

  private refused(code: string | null): void {
    if (code === 'two-factor-required') {
      this.step.set('second-factor');
    } else {
      this.error.set(adminLoginErrorKey(code));
    }
  }
}
