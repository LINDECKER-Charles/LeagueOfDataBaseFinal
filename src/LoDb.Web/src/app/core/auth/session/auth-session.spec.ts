import { HttpErrorResponse } from '@angular/common/http';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { EMPTY, throwError } from 'rxjs';
import { AUTH_STRATEGY } from '../strategy/auth-strategy-token';
import { accountUser } from '../testing/account-user';
import { FakeAuthStrategy } from '../testing/fake-auth-strategy';
import { AuthSession } from './auth-session';
import { hasAdminAccess } from './has-admin-access';

const CREDENTIALS = { identifier: 'Faker', password: 'correct horse battery staple' };

interface Setup {
  readonly session: AuthSession;
  readonly strategy: FakeAuthStrategy;
}

function setUp(platform: 'browser' | 'server' = 'browser'): Setup {
  const strategy = new FakeAuthStrategy();
  TestBed.configureTestingModule({
    providers: [
      { provide: AUTH_STRATEGY, useValue: strategy },
      { provide: PLATFORM_ID, useValue: platform },
    ],
  });
  return { session: TestBed.inject(AuthSession), strategy };
}

describe('AuthSession', () => {
  it('knows nothing until /api/account/me has answered', async () => {
    const { session, strategy } = setUp();

    const loading = session.load();

    expect(session.status()).toBe('unknown');
    expect(session.user()).toBeNull();
    strategy.answerRead(accountUser());
    await expect(loading).resolves.toEqual(accountUser());
    expect(session.status()).toBe('authenticated');
    expect(session.isAuthenticated()).toBe(true);
  });

  it('reads the session once, whoever asks', async () => {
    const { session, strategy } = setUp();

    const first = session.load();
    const second = session.load();
    strategy.answerRead(null);

    await expect(Promise.all([first, second])).resolves.toEqual([null, null]);
    await expect(session.load()).resolves.toBeNull();
    expect(strategy.reads).toHaveLength(1);
    expect(session.status()).toBe('anonymous');
  });

  it('reads it again on refresh, after an action that changed the account', async () => {
    const { session, strategy } = setUp();
    const loading = session.load();
    strategy.answerRead(accountUser({ emailVerified: false }));
    await loading;

    const refreshing = session.refresh();
    strategy.answerRead(accountUser());

    await expect(refreshing).resolves.toEqual(accountUser());
    expect(strategy.reads).toHaveLength(2);
    expect(session.isEmailVerified()).toBe(true);
  });

  it('stays unknown when the read fails, and asks again next time', async () => {
    const { session, strategy } = setUp();
    const failure = new HttpErrorResponse({ status: 503 });

    const loading = session.load();
    strategy.failRead(failure);

    await expect(loading).rejects.toBe(failure);
    expect(session.status()).toBe('unknown');
    const retry = session.load();
    strategy.answerRead(null);
    await expect(retry).resolves.toBeNull();
    expect(strategy.reads).toHaveLength(2);
  });

  it('never reads during a server render, which stays anonymous', async () => {
    const { session, strategy } = setUp('server');

    await expect(session.load()).resolves.toBeNull();
    await expect(session.refresh()).resolves.toBeNull();

    expect(strategy.reads).toHaveLength(0);
    expect(session.status()).toBe('unknown');
  });

  it('adopts the session of a sign-in, then lands on the safe return URL', async () => {
    const { session } = setUp();

    const landing = await session.signIn(CREDENTIALS, {
      returnUrl: '/fr/account/builds',
      fallbackUrl: '/fr/account/profile',
    });

    expect(landing).toBe('/fr/account/builds');
    expect(session.user()).toEqual(accountUser());
    expect(session.status()).toBe('authenticated');
  });

  it('keeps the session as it was when the sign-in is refused', async () => {
    const { session, strategy } = setUp();
    const refusal = new HttpErrorResponse({ status: 401 });
    strategy.signInAnswer = throwError(() => refusal);

    await expect(
      session.signIn(CREDENTIALS, { returnUrl: null, fallbackUrl: '/fr/account/profile' }),
    ).rejects.toBe(refusal);
    expect(session.status()).toBe('unknown');
  });

  it('lets no read started before a sign-in undo it', async () => {
    const { session, strategy } = setUp();
    const loading = session.load();

    await session.signIn(CREDENTIALS, { returnUrl: null, fallbackUrl: '/fr/' });
    strategy.answerRead(null);

    await expect(loading).resolves.toEqual(accountUser());
    expect(session.isAuthenticated()).toBe(true);
  });

  it('becomes anonymous on sign-out, and stays signed in when it fails', async () => {
    const { session, strategy } = setUp();
    session.apply({ user: accountUser() });
    strategy.signOutAnswer = throwError(() => new HttpErrorResponse({ status: 503 }));

    await expect(session.signOut()).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(session.isAuthenticated()).toBe(true);

    strategy.signOutAnswer = EMPTY;
    await session.signOut();
    expect(session.status()).toBe('anonymous');
    expect(session.user()).toBeNull();
  });

  it('leaves for Google through the strategy', async () => {
    const { session, strategy } = setUp();
    const request = { returnUrl: '/fr/', fallbackUrl: '/fr/', locale: 'fr', rememberMe: true };

    await session.startGoogleSignIn(request);

    expect(strategy.googleSignIns).toEqual([request]);
  });

  it('opens the admin to an administrator signed in with a second factor only', () => {
    const { session } = setUp();

    session.apply({ user: accountUser({ roles: ['Admin'], multiFactor: false }) });
    expect(session.isAdmin()).toBe(false);

    session.apply({ user: accountUser({ roles: ['Admin'], multiFactor: true }) });
    expect(session.isAdmin()).toBe(true);
  });
});

describe('hasAdminAccess', () => {
  it.each([
    [null, false],
    [accountUser(), false],
    [accountUser({ multiFactor: true }), false],
    [accountUser({ roles: ['Admin'] }), false],
    [accountUser({ roles: ['Admin'], multiFactor: true }), true],
  ])('%j: %s', (user, expected) => {
    expect(hasAdminAccess(user)).toBe(expected);
  });
});
