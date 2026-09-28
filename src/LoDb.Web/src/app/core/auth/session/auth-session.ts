import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, type Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import type { AccountSession } from '../../api/generated/models/account-session';
import type { AccountUser } from '../../api/generated/models/account-user';
import type { LoginRequest } from '../../api/generated/models/login-request';
import { AUTH_STRATEGY } from '../strategy/auth-strategy-token';
import type { GoogleSignIn } from '../strategy/google-sign-in';
import { hasAdminAccess } from './has-admin-access';
import type { SessionStatus } from './session-status';
import type { SignInTarget } from './sign-in-target';

interface SessionState {
  readonly status: SessionStatus;
  readonly user: AccountUser | null;
}

function stateOf(user: AccountUser | null): SessionState {
  return { status: user === null ? 'anonymous' : 'authenticated', user };
}

/**
 * Who is signed in, for every page and every platform: `/api/account/me` read through the
 * platform's `AuthStrategy`, once, then kept up to date by the sign-in and the sign-out.
 * Never read during a server render: the SSR stays anonymous and `status` stays `unknown`
 * there. `provideAuth` starts the first read in the browser after the first render; a guard
 * awaits it through `load()`.
 */
@Injectable({ providedIn: 'root' })
export class AuthSession {
  private readonly strategy = inject(AUTH_STRATEGY);
  private readonly inBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly state = signal<SessionState>({ status: 'unknown', user: null });
  private reading: Promise<AccountUser | null> | null = null;
  // Bumped by every change of session, so that a read started before it cannot undo it.
  private revision = 0;

  readonly user: Signal<AccountUser | null> = computed(() => this.state().user);
  readonly status: Signal<SessionStatus> = computed(() => this.state().status);
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly isEmailVerified = computed(() => this.user()?.emailVerified === true);
  readonly isAdmin = computed(() => hasAdminAccess(this.user()));

  /** The user once the session is known: reads it the first time, answers at once after. */
  load(): Promise<AccountUser | null> {
    return this.status() === 'unknown' ? this.refresh() : Promise.resolve(this.user());
  }

  /**
   * Reads the session again, after an action that changes the account (e-mail verified,
   * profile saved). Concurrent calls share one request; a failed one leaves the state as it
   * was and rejects with the `HttpErrorResponse`. Null at once on the server.
   */
  refresh(): Promise<AccountUser | null> {
    if (!this.inBrowser) {
      return Promise.resolve(null);
    }
    this.reading ??= this.read().finally(() => (this.reading = null));
    return this.reading;
  }

  /** Adopts the session an account call answered (registration…), without reading it again. */
  apply(session: AccountSession): void {
    this.revision++;
    this.state.set(stateOf(session.user));
  }

  /**
   * Signs in, then answers where to land, root-relative. A refusal rejects with the API's
   * `HttpErrorResponse` (`two-factor-required`: sign in again with `twoFactorCode`).
   */
  async signIn(credentials: LoginRequest, target: SignInTarget): Promise<string> {
    this.apply(await firstValueFrom(this.strategy.signIn(credentials)));
    return this.strategy.landingUrl(target.returnUrl, target.fallbackUrl);
  }

  /** Signs out; the session stays as it was when the call fails. */
  async signOut(): Promise<void> {
    await firstValueFrom(this.strategy.signOut(), { defaultValue: undefined });
    this.apply({ user: null });
  }

  /** Leaves for Google's sign-in, the way the platform does it. */
  startGoogleSignIn(request: GoogleSignIn): Promise<void> {
    return this.strategy.startGoogleSignIn(request);
  }

  private async read(): Promise<AccountUser | null> {
    const revision = this.revision;
    const session = await firstValueFrom(this.strategy.readSession());
    if (revision === this.revision) {
      this.apply(session);
    }
    return this.user();
  }
}
