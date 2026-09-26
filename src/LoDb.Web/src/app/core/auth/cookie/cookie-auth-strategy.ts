import {
  HttpClient,
  type HttpEvent,
  type HttpHandlerFn,
  type HttpRequest,
} from '@angular/common/http';
import { DOCUMENT, Injectable, inject } from '@angular/core';
import { type Observable, map } from 'rxjs';
import { API_BASE_URL } from '../../api/api-base-url';
import { getAccountSession } from '../../api/generated/fn/account/get-account-session';
import { signIn } from '../../api/generated/fn/account/sign-in';
import { signOut } from '../../api/generated/fn/account/sign-out';
import type { AccountSession } from '../../api/generated/models/account-session';
import type { LoginRequest } from '../../api/generated/models/login-request';
import { safeReturnUrl } from '../../routing/safe-return-url';
import type { AuthStrategy } from '../strategy/auth-strategy';
import type { GoogleSignIn } from '../strategy/google-sign-in';
import { googleSignInUrl } from './google-sign-in-url';

/**
 * The web (ADR 0009): an `HttpOnly` session cookie the browser keeps and sends by itself to
 * its own origin, so requests leave as they are. The XSRF token is `HttpClient`'s: the API
 * sets the `XSRF-TOKEN` cookie on `/api/account/me`, sign-in and sign-out, and `HttpClient`
 * copies it into `X-XSRF-TOKEN` on the unsafe same-origin requests. It calls the generated
 * operations rather than `AccountService`, which would bring every account call into the
 * initial bundle.
 */
@Injectable({ providedIn: 'root' })
export class CookieAuthStrategy implements AuthStrategy {
  private readonly http = inject(HttpClient);
  private readonly apiOrigin = inject(API_BASE_URL);
  private readonly document = inject(DOCUMENT);

  readSession(): Observable<AccountSession> {
    return getAccountSession(this.http, this.apiOrigin).pipe(map(({ body }) => body));
  }

  signIn(credentials: LoginRequest): Observable<AccountSession> {
    return signIn(this.http, this.apiOrigin, { body: credentials }).pipe(map(({ body }) => body));
  }

  signOut(): Observable<void> {
    return signOut(this.http, this.apiOrigin).pipe(map(() => undefined));
  }

  async startGoogleSignIn(request: GoogleSignIn): Promise<void> {
    const url = googleSignInUrl(this.apiOrigin, {
      ReturnUrl: this.landingUrl(request.returnUrl, request.fallbackUrl),
      Locale: request.locale,
      RememberMe: request.rememberMe,
    });
    this.document.location.assign(url);
  }

  landingUrl(returnUrl: string | null | undefined, fallbackUrl: string): string {
    return safeReturnUrl(returnUrl, this.document.location.origin, fallbackUrl);
  }

  authorize(request: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> {
    return next(request);
  }
}
