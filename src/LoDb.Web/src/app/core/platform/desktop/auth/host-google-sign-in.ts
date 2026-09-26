import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DOCUMENT, Injectable, Injector, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthSession } from '../../../auth/session/auth-session';
import type { GoogleSignIn } from '../../../auth/strategy/google-sign-in';
import { DEFAULT_LOCALE } from '../../../i18n/default-locale';
import { isLocale } from '../../../i18n/is-locale';
import { localePath } from '../../../layout/shell/locale-path';
import { HostAuthRoutes } from './host-auth-routes';
import type { HostGoogleStatus } from './host-google-status';
import { loginErrorCode } from './login-error-code';
import { readHostGoogleStatus } from './read-host-google-status';

/** How often the page asks the host whether the sign-in in the browser has ended. */
export const GOOGLE_POLL_MS = 1_000;
/** The host gives the user ten minutes on Google's page; a minute more covers the exchange. */
export const GOOGLE_DEADLINE_MS = 11 * 60_000;
const LOGIN_PAGE = 'account/login';
const ERROR_PARAM = 'error';
const GOOGLE_FAILED = 'google-failed';

const pause = (ms: number): Promise<void> => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Google's sign-in on desktop (ADR 0009): Google refuses embedded WebViews, so the host
 * opens its page in the system browser and redeems the code on its loopback redirect. The
 * page only starts the flow and polls the host's session until the flow ends, then lands
 * signed in, or on the login page with the reason. A new start replaces a running one, as
 * on the host: the one it replaced stops polling.
 */
@Injectable({ providedIn: 'root' })
export class HostGoogleSignIn {
  private readonly http = inject(HttpClient);
  private readonly origin = inject(DOCUMENT).location.origin;
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private flow = 0;

  /**
   * Resolves once Google's page is open, so that the button can be used again should the
   * user close the browser; the outcome is awaited in the background.
   */
  async start(request: GoogleSignIn, landingUrl: string): Promise<void> {
    const flow = ++this.flow;
    const loginPage = localePath(
      isLocale(request.locale) ? request.locale : DEFAULT_LOCALE,
      LOGIN_PAGE,
    );
    try {
      await firstValueFrom(
        this.http.get(`${this.origin}${HostAuthRoutes.google}`, {
          params: { rememberMe: request.rememberMe },
        }),
      );
    } catch (error) {
      await this.fail(loginPage, problemCodeOf(error));
      return;
    }
    void this.follow(flow, loginPage, landingUrl);
  }

  private async follow(flow: number, loginPage: string, landingUrl: string): Promise<void> {
    const outcome = await this.outcome(flow);
    if (outcome === null) {
      return;
    }
    if (outcome.stage === 'succeeded') {
      // The host holds the tokens: a failed read only delays the session to the next guard.
      await this.injector
        .get(AuthSession)
        .refresh()
        .catch(() => null);
      await this.router.navigateByUrl(landingUrl);
      return;
    }
    // Still pending past the deadline: the host has let the flow expire by now.
    await this.fail(
      loginPage,
      outcome.stage === 'failed' ? loginErrorCode(outcome.failure) : GOOGLE_FAILED,
    );
  }

  // Null once a newer flow replaced this one; `pending` past the deadline.
  private async outcome(flow: number): Promise<HostGoogleStatus | null> {
    const deadline = Date.now() + GOOGLE_DEADLINE_MS;
    while (Date.now() < deadline) {
      await pause(GOOGLE_POLL_MS);
      if (flow !== this.flow) {
        return null;
      }
      const status = await this.readStatus();
      if (status.stage !== 'pending') {
        return status;
      }
    }
    return { stage: 'pending' };
  }

  // A failed read is retried at the next poll: the host may be busy with the exchange.
  private async readStatus(): Promise<HostGoogleStatus> {
    const session = this.http.get<unknown>(`${this.origin}${HostAuthRoutes.session}`);
    return firstValueFrom(session).then(readHostGoogleStatus, () => ({ stage: 'pending' }));
  }

  private async fail(loginPage: string, code: string): Promise<void> {
    await this.router.navigateByUrl(
      this.router.createUrlTree([loginPage], {
        queryParams: { [ERROR_PARAM]: code },
      }),
    );
  }
}

/** The ProblemDetails `code` of a refused start, as the login page words it. */
function problemCodeOf(error: unknown): string {
  const problem: unknown = error instanceof HttpErrorResponse ? error.error : null;
  return typeof problem === 'object' &&
    problem !== null &&
    'code' in problem &&
    typeof problem.code === 'string'
    ? loginErrorCode(problem.code)
    : GOOGLE_FAILED;
}
