import type { HttpEvent, HttpHandlerFn, HttpRequest } from '@angular/common/http';
import { type Observable, Subject, of } from 'rxjs';
import type { AccountSession } from '../../api/generated/models/account-session';
import type { AccountUser } from '../../api/generated/models/account-user';
import type { LoginRequest } from '../../api/generated/models/login-request';
import type { AuthStrategy } from '../strategy/auth-strategy';
import type { GoogleSignIn } from '../strategy/google-sign-in';
import { accountUser } from './account-user';

/**
 * An `AuthStrategy` whose calls the spec answers: each `readSession()` opens a subject in
 * `reads`, completed by `answerRead`, unless `sessionAnswer` is set; the sign-in and sign-out
 * answer what the spec set.
 * `authorize` tags the request it forwards, so a spec sees it went through.
 */
export class FakeAuthStrategy implements AuthStrategy {
  readonly reads: Subject<AccountSession>[] = [];
  readonly authorized: HttpRequest<unknown>[] = [];
  readonly googleSignIns: GoogleSignIn[] = [];
  signInAnswer: Observable<AccountSession> = of({ user: accountUser() });
  signOutAnswer: Observable<void> = of(undefined);
  sessionAnswer: Observable<AccountSession> | null = null;

  readSession(): Observable<AccountSession> {
    if (this.sessionAnswer !== null) {
      return this.sessionAnswer;
    }
    const read = new Subject<AccountSession>();
    this.reads.push(read);
    return read;
  }

  signIn(_credentials: LoginRequest): Observable<AccountSession> {
    return this.signInAnswer;
  }

  signOut(): Observable<void> {
    return this.signOutAnswer;
  }

  async startGoogleSignIn(request: GoogleSignIn): Promise<void> {
    this.googleSignIns.push(request);
  }

  landingUrl(returnUrl: string | null | undefined, fallbackUrl: string): string {
    return returnUrl?.startsWith('/') ? returnUrl : fallbackUrl;
  }

  authorize(request: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> {
    this.authorized.push(request);
    return next(request.clone({ setHeaders: { Authorization: 'Bearer fake' } }));
  }

  /** Answers the pending read at `index` (the last one by default) and completes it. */
  answerRead(user: AccountUser | null, index = this.reads.length - 1): void {
    const read = this.reads[index];
    read?.next({ user });
    read?.complete();
  }

  /** Fails the pending read at `index` (the last one by default). */
  failRead(error: unknown, index = this.reads.length - 1): void {
    this.reads[index]?.error(error);
  }
}
