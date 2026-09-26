import type { HttpEvent, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import type { Observable } from 'rxjs';
import type { AccountSession } from '../../api/generated/models/account-session';
import type { LoginRequest } from '../../api/generated/models/login-request';
import type { GoogleSignIn } from './google-sign-in';

/**
 * How a platform signs in and authenticates its API requests (plan, section 5.2; ADR 0009):
 * session cookie on the web (`cookie`), tokens held by the desktop host (`host`), bearer
 * tokens with a refresh in secure storage on Android (`bearer`). `AuthSession`, the guards
 * and the interceptor only talk to this interface; the platform picks the implementation
 * through `AuthStrategies`. A refused call errors with the API's `HttpErrorResponse`, whose
 * ProblemDetails `code` (`invalid-credentials`, `two-factor-required`…) the pages switch on.
 */
export interface AuthStrategy {
  /** The session of the caller, as `GET /api/account/me` answers it. */
  readSession(): Observable<AccountSession>;

  /** Opens a session with an identifier and a password, and answers it. */
  signIn(credentials: LoginRequest): Observable<AccountSession>;

  /** Closes the session. */
  signOut(): Observable<void>;

  /** Leaves for Google's sign-in: a full-page navigation, or the system browser. */
  startGoogleSignIn(request: GoogleSignIn): Promise<void>;

  /**
   * Where a completed sign-in lands, root-relative: `returnUrl` when it stays in the
   * application, `fallbackUrl` otherwise (anyone can forge a return URL).
   */
  landingUrl(returnUrl: string | null | undefined, fallbackUrl: string): string;

  /**
   * Authenticates an API request and sends it through `next`. Only requests to the API reach
   * it, never during a server render. The whole exchange is in its hands, so that a token
   * strategy can refresh and send again after a 401.
   */
  authorize(request: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>>;
}
