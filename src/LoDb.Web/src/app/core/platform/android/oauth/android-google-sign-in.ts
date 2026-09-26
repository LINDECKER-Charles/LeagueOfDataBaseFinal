import { HttpClient } from '@angular/common/http';
import { Injectable, Injector, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../../api/api-base-url';
import { exchangeGoogleCode } from '../../../api/generated/fn/account/exchange-google-code';
import type { AccessTokenResponse } from '../../../api/generated/models/access-token-response';
import { AuthSession } from '../../../auth/session/auth-session';
import type { GoogleSignIn } from '../../../auth/strategy/google-sign-in';
import { DEFAULT_LOCALE } from '../../../i18n/default-locale';
import { isLocale } from '../../../i18n/is-locale';
import { localePath } from '../../../layout/shell/locale-path';
import { BearerTokens } from '../auth/bearer-tokens';
import { bypassContext } from '../auth/bypass-context';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { afterFirstNavigation } from './after-first-navigation';
import { ANDROID_GOOGLE_CLIENT } from './android-google-client-token';
import { googleAuthorizationUrl } from './google-authorization-url';
import { googleFailureCode } from './google-failure-code';
import type { GoogleReturn } from './google-return';
import { parseGoogleReturn } from './parse-google-return';
import type { PendingGoogleSignIn } from './pending-google-sign-in';
import { PendingGoogleSignIns } from './pending-google-sign-ins';
import { pkceChallenge } from './pkce-challenge';
import { pkceSecret } from './pkce-secret';

// The login page and the `error` codes it translates, as the API's web callback sends them.
const LOGIN_PAGE = 'account/login';
const ERROR_PARAM = 'error';
const GOOGLE_UNAVAILABLE = 'google-unavailable';
const GOOGLE_CANCELLED = 'google-cancelled';
const GOOGLE_FAILED = 'google-failed';
// What Google sends back when the user declines the consent screen.
const ACCESS_DENIED = 'access_denied';

/**
 * Google's sign-in on Android (ADR 0009): Google refuses embedded WebViews, so the app opens
 * its page in the system browser with PKCE, Google redirects to a verified App Link, and the
 * API redeems the code (`/api/account/google/app/exchange`) for the tokens of the account.
 * The app never calls Google itself, and the client secret stays on the API.
 */
@Injectable({ providedIn: 'root' })
export class AndroidGoogleSignIn {
  private readonly plugins = inject(ANDROID_PLUGINS);
  private readonly client = inject(ANDROID_GOOGLE_CLIENT);
  private readonly pending = inject(PendingGoogleSignIns);
  private readonly tokens = inject(BearerTokens);
  private readonly http = inject(HttpClient);
  private readonly apiOrigin = inject(API_BASE_URL);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);

  /** Opens Google's page in the system browser; the App Link brings the answer back. */
  async start(request: GoogleSignIn, landingUrl: string): Promise<void> {
    const { clientId, redirectUri } = this.client;
    if (clientId === null) {
      await this.fail(request.locale, GOOGLE_UNAVAILABLE);
      return;
    }
    const flow: PendingGoogleSignIn = {
      clientId,
      redirectUri,
      state: pkceSecret(),
      verifier: pkceSecret(),
      landingUrl,
      locale: request.locale,
      isRemembered: request.rememberMe,
      startedAt: Date.now(),
    };
    await this.pending.save(flow);
    const challenge = await pkceChallenge(flow.verifier);
    await this.plugins.browser.open({ url: googleAuthorizationUrl(flow, challenge) });
  }

  /** Completes the sign-in an App Link brings back; any other URL is left alone. */
  async complete(url: string): Promise<void> {
    const answer = parseGoogleReturn(url, this.client.redirectUri);
    if (answer === null) {
      return;
    }
    await this.plugins.browser.close().catch(() => undefined);
    const flow = await this.pending.take();
    await afterFirstNavigation(this.router);
    if (flow === null || flow.state !== answer.state) {
      // Expired, already used, or not asked by this app: a forged link signs no one in.
      await this.fail(flow?.locale ?? DEFAULT_LOCALE, GOOGLE_FAILED);
      return;
    }
    await this.redeem(flow, answer);
  }

  private async redeem(flow: PendingGoogleSignIn, answer: GoogleReturn): Promise<void> {
    if (answer.code === null) {
      await this.fail(
        flow.locale,
        answer.error === ACCESS_DENIED ? GOOGLE_CANCELLED : GOOGLE_FAILED,
      );
      return;
    }
    try {
      await this.tokens.adopt(await this.exchange(flow, answer.code), flow.isRemembered);
    } catch (error) {
      await this.fail(flow.locale, googleFailureCode(error));
      return;
    }
    // The tokens are kept: a failed read only delays the session to the next guard.
    await this.injector
      .get(AuthSession)
      .refresh()
      .catch(() => null);
    await this.router.navigateByUrl(flow.landingUrl);
  }

  private async exchange(flow: PendingGoogleSignIn, code: string): Promise<AccessTokenResponse> {
    const body = {
      clientId: flow.clientId,
      code,
      codeVerifier: flow.verifier,
      redirectUri: flow.redirectUri,
    };
    const exchange = exchangeGoogleCode(this.http, this.apiOrigin, { body }, bypassContext());
    return (await firstValueFrom(exchange)).body;
  }

  private async fail(locale: string, code: string): Promise<void> {
    const page = localePath(isLocale(locale) ? locale : DEFAULT_LOCALE, LOGIN_PAGE);
    await this.router.navigateByUrl(
      this.router.createUrlTree([page], { queryParams: { [ERROR_PARAM]: code } }),
    );
  }
}
