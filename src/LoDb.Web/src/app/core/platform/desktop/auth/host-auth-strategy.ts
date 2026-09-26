import {
  HttpClient,
  type HttpEvent,
  type HttpHandlerFn,
  type HttpRequest,
} from '@angular/common/http';
import { DOCUMENT, Injectable, inject } from '@angular/core';
import { type Observable, map, switchMap } from 'rxjs';
import { API_BASE_URL } from '../../../api/api-base-url';
import { getAccountSession } from '../../../api/generated/fn/account/get-account-session';
import type { AccountSession } from '../../../api/generated/models/account-session';
import type { LoginRequest } from '../../../api/generated/models/login-request';
import type { AuthStrategy } from '../../../auth/strategy/auth-strategy';
import type { GoogleSignIn } from '../../../auth/strategy/google-sign-in';
import { safeReturnUrl } from '../../../routing/safe-return-url';
import { HostAuthRoutes } from './host-auth-routes';
import { HostGoogleSignIn } from './host-google-sign-in';

/**
 * Desktop (ADR 0009, plan section 5.2): the host holds the tokens, the refresh one
 * encrypted on disk when the session is remembered, and adds the access token to the API
 * requests it relays. The page never sees a token: it signs in and out through the host's
 * `/desktop/auth/*` endpoints, which relay the API's ProblemDetails on a refusal, and its
 * API requests leave as they are. Registered as `host` by the desktop platform.
 */
@Injectable({ providedIn: 'root' })
export class HostAuthStrategy implements AuthStrategy {
  private readonly http = inject(HttpClient);
  private readonly apiOrigin = inject(API_BASE_URL);
  private readonly document = inject(DOCUMENT);
  private readonly google = inject(HostGoogleSignIn);

  /** Through the host's proxy, which answers anonymous once it holds no token. */
  readSession(): Observable<AccountSession> {
    return getAccountSession(this.http, this.apiOrigin).pipe(map(({ body }) => body));
  }

  signIn(credentials: LoginRequest): Observable<AccountSession> {
    return this.http
      .post(this.hostUrl(HostAuthRoutes.login), credentials)
      .pipe(switchMap(() => this.readSession()));
  }

  signOut(): Observable<void> {
    return this.http.post(this.hostUrl(HostAuthRoutes.logout), null).pipe(map(() => undefined));
  }

  startGoogleSignIn(request: GoogleSignIn): Promise<void> {
    return this.google.start(request, this.landingUrl(request.returnUrl, request.fallbackUrl));
  }

  landingUrl(returnUrl: string | null | undefined, fallbackUrl: string): string {
    return safeReturnUrl(returnUrl, this.document.location.origin, fallbackUrl);
  }

  /** The host adds the token to what it relays: the request leaves as it is. */
  authorize(request: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> {
    return next(request);
  }

  private hostUrl(path: string): string {
    return `${this.document.location.origin}${path}`;
  }
}
