import { HttpClient } from '@angular/common/http';
import { Injectable, Injector, inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../../core/api/api-base-url';
import { resendVerificationEmail } from '../../../core/api/generated/fn/account/resend-verification-email';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { PageDirection } from '../../../core/layout/direction/page-direction';
import type { ToastKind } from '../../../core/layout/toast/toast-kind';
import { ToastService } from '../../../core/layout/toast/toast-service';
import { accountMessage } from './account-message';
import { problemOf } from './problems/problem-of';

/** What each answer to a resend says, and how loud. */
const OUTCOMES: Readonly<Record<string, readonly [ToastKind, string]>> = {
  sent: ['success', 'auth.verify.resend_done'],
  'resend-throttled': ['warning', 'auth.verify.resend_throttled'],
  'email-already-verified': ['info', 'auth.flash.verify_already'],
};
const FAILED: readonly [ToastKind, string] = ['error', 'account.errors.generic'];

/**
 * Sends the verification e-mail again, in the locale of the page, and says how it went in a
 * toast. The banner of every page uses it: it calls the generated operation rather than
 * `AccountService`, which would bring every account call into the initial bundle, and reads
 * the session only when an answer changes it.
 */
@Injectable({ providedIn: 'root' })
export class ResendVerification {
  private readonly http = inject(HttpClient);
  private readonly apiOrigin = inject(API_BASE_URL);
  private readonly injector = inject(Injector);
  private readonly page = inject(PageDirection);
  private readonly toasts = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  /** Sends the e-mail and tells how it went; never fails. */
  async send(): Promise<void> {
    const locale = this.page.locale();
    let outcome: string | null = 'sent';
    try {
      await firstValueFrom(
        resendVerificationEmail(this.http, this.apiOrigin, { body: { locale } }),
      );
    } catch (error) {
      outcome = problemOf(error).code;
    }
    if (outcome === 'email-already-verified') {
      // Verified in another tab: the banner goes once the session says so.
      await this.injector
        .get(AuthSession)
        .refresh()
        .catch(() => null);
    }
    const [kind, key] = OUTCOMES[outcome ?? ''] ?? FAILED;
    this.toasts.show(kind, await accountMessage(this.transloco, locale, key));
  }
}
