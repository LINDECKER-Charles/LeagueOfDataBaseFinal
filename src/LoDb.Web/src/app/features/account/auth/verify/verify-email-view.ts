import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { AccountService } from '../../../../core/api/generated/services/account.service';
import { RETURN_URL_PARAM } from '../../../../core/auth/guards/return-url-param';
import { AuthSession } from '../../../../core/auth/session/auth-session';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { localePath } from '../../../../core/layout/shell/locale-path';
import { safeReturnUrl } from '../../../../core/routing/safe-return-url';
import { Button } from '../../../../ui/controls/button';
import { problemOf } from '../../shared/problems/problem-of';
import { ResendVerification } from '../../shared/resend-verification';
import { AuthCard } from '../card/auth-card';
import type { VerifyOutcome } from './verify-outcome';

// The e-mail link: `verify-email?user={id}&token={token}`.
const USER_PARAM = 'user';
const TOKEN_PARAM = 'token';
const USER_ID = /^[1-9][0-9]*$/;
const SETTLED: readonly VerifyOutcome[] = ['verified', 'already'];

/**
 * The page of the verification link, which checks it at once, and the page the
 * verified-email guard sends to: it tells how to finish, sends the e-mail again for a
 * signed-in account, then goes on to the page asked for.
 */
@Component({
  selector: 'lodb-verify-email-view',
  imports: [AuthCard, Button, RouterLink, TranslocoPipe],
  templateUrl: './verify-email-view.html',
  styleUrl: '../card/auth-form.css',
  // Grows with the account page, so its card is centred between header and footer.
  host: { class: 'flex flex-1 flex-col' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VerifyEmailView {
  private readonly account = inject(AccountService);
  private readonly session = inject(AuthSession);
  private readonly page = inject(PageDirection);
  private readonly query = inject(ActivatedRoute).snapshot.queryParamMap;
  private readonly origin = inject(DOCUMENT).location.origin;

  private readonly resendVerification = inject(ResendVerification);
  protected readonly sending = signal(false);
  protected readonly outcome = signal<VerifyOutcome>('waiting');
  protected readonly user = this.session.user;
  protected readonly status = this.session.status;
  /** Verified by the link or before it: the page asked for is open now. */
  protected readonly done = computed(
    () => SETTLED.includes(this.outcome()) || this.session.isEmailVerified(),
  );
  protected readonly next = computed(() =>
    safeReturnUrl(this.query.get(RETURN_URL_PARAM), this.origin, this.path('account/profile')),
  );
  protected readonly login = computed(() => this.path('account/login'));
  protected readonly loginQuery = computed(() => ({
    [RETURN_URL_PARAM]: this.path('account/verify-email'),
  }));

  constructor() {
    const user = this.query.get(USER_PARAM) ?? '';
    const token = this.query.get(TOKEN_PARAM) ?? '';
    if (USER_ID.test(user) && token !== '') {
      void this.verify(Number(user), token);
    }
  }

  protected async resend(): Promise<void> {
    this.sending.set(true);
    await this.resendVerification.send();
    this.sending.set(false);
  }

  private path(path: string): string {
    return localePath(this.page.locale(), path);
  }

  private async verify(userId: number, token: string): Promise<void> {
    this.outcome.set('verifying');
    try {
      const body = { userId, token };
      const { alreadyVerified } = await firstValueFrom(this.account.verifyEmail({ body }));
      this.outcome.set(alreadyVerified ? 'already' : 'verified');
      // The banner and the guards read the session: it now says verified.
      await this.session.refresh().catch(() => null);
    } catch (error) {
      this.outcome.set(problemOf(error).code === 'invalid-token' ? 'invalid' : 'failed');
    }
  }
}
