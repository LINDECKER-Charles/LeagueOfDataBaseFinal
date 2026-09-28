import {
  HttpClient,
  HttpErrorResponse,
  type HttpEvent,
  type HttpHandlerFn,
  type HttpRequest,
  HttpStatusCode,
} from '@angular/common/http';
import { DOCUMENT, Injectable, inject } from '@angular/core';
import { type Observable, catchError, from, map, of, switchMap, throwError } from 'rxjs';
import { API_BASE_URL } from '../../../api/api-base-url';
import { createAccountToken } from '../../../api/generated/fn/account/create-account-token';
import { getAccountSession } from '../../../api/generated/fn/account/get-account-session';
import type { AccountSession } from '../../../api/generated/models/account-session';
import type { LoginRequest } from '../../../api/generated/models/login-request';
import type { AuthStrategy } from '../../../auth/strategy/auth-strategy';
import type { GoogleSignIn } from '../../../auth/strategy/google-sign-in';
import { safeReturnUrl } from '../../../routing/safe-return-url';
import { AndroidGoogleSignIn } from '../oauth/android-google-sign-in';
import { BEARER_BYPASS } from './bearer-bypass';
import { BearerTokens } from './bearer-tokens';
import { bypassContext } from './bypass-context';

const ANONYMOUS: AccountSession = { user: null };

function withToken(request: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

function isUnauthorized(error: unknown): error is HttpErrorResponse {
  return error instanceof HttpErrorResponse && error.status === HttpStatusCode.Unauthorized;
}

/**
 * Android (ADR 0009): no cookie crosses from the WebView's https://localhost to the API, so
 * the app signs in for tokens (`/api/account/token`) and sends the access token in the
 * `Authorization` header. An expired token is renewed before the request leaves; a 401 is
 * answered by one renewal and one more attempt. Registered as `bearer` by the platform.
 */
@Injectable({ providedIn: 'root' })
export class BearerAuthStrategy implements AuthStrategy {
  private readonly http = inject(HttpClient);
  private readonly apiOrigin = inject(API_BASE_URL);
  private readonly document = inject(DOCUMENT);
  private readonly tokens = inject(BearerTokens);
  private readonly google = inject(AndroidGoogleSignIn);

  /** Without tokens the caller is anonymous: no request leaves to be told so. */
  readSession(): Observable<AccountSession> {
    return from(this.tokens.hasSession()).pipe(
      switchMap((hasSession) =>
        hasSession
          ? getAccountSession(this.http, this.apiOrigin).pipe(map(({ body }) => body))
          : of(ANONYMOUS),
      ),
    );
  }

  /**
   * The token endpoint takes no "remember me": it decides here whether the refresh token
   * outlives the app, as the cookie outlives the browser on the web.
   */
  signIn(credentials: LoginRequest): Observable<AccountSession> {
    const { rememberMe, ...body } = credentials;
    return createAccountToken(this.http, this.apiOrigin, { body }, bypassContext()).pipe(
      switchMap(({ body: tokens }) => this.tokens.adopt(tokens, rememberMe === true)),
      switchMap(() => this.readSession()),
    );
  }

  signOut(): Observable<void> {
    return from(this.tokens.clear());
  }

  startGoogleSignIn(request: GoogleSignIn): Promise<void> {
    return this.google.start(request, this.landingUrl(request.returnUrl, request.fallbackUrl));
  }

  landingUrl(returnUrl: string | null | undefined, fallbackUrl: string): string {
    return safeReturnUrl(returnUrl, this.document.location.origin, fallbackUrl);
  }

  authorize(request: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> {
    if (request.context.get(BEARER_BYPASS)) {
      return next(request);
    }
    return from(this.tokens.accessToken()).pipe(
      switchMap((token) => (token === null ? next(request) : this.send(request, next, token))),
    );
  }

  private send(
    request: HttpRequest<unknown>,
    next: HttpHandlerFn,
    token: string,
  ): Observable<HttpEvent<unknown>> {
    return next(withToken(request, token)).pipe(
      catchError((error: unknown) => {
        if (!isUnauthorized(error)) {
          return throwError(() => error);
        }
        // Renewed once: a second 401 is the API's answer, not a stale token.
        return from(this.tokens.replace(token)).pipe(
          switchMap((renewed) =>
            renewed === null ? throwError(() => error) : next(withToken(request, renewed)),
          ),
        );
      }),
    );
  }
}
