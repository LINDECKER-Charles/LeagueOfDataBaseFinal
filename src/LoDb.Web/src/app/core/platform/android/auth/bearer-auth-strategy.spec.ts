import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  type TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../../api/api-base-url';
import type { AccessTokenResponse } from '../../../api/generated/models/access-token-response';
import { authInterceptor } from '../../../auth/auth-interceptor';
import { AUTH_STRATEGY } from '../../../auth/strategy/auth-strategy-token';
import { accountUser } from '../../../auth/testing/account-user';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { FakeAndroidPlugins } from '../testing/fake-android-plugins';
import { BearerAuthStrategy } from './bearer-auth-strategy';
import { BearerTokens } from './bearer-tokens';

const API = 'https://league-of-data-base.com';
const ME_URL = `${API}/api/account/me`;
const TOKEN_URL = `${API}/api/account/token`;
const REFRESH_URL = `${API}/api/account/refresh`;
const BUILDS_URL = `${API}/api/builds`;
const STORED_KEY = 'lodb.refresh-token';

function tokens(access: string, refresh: string): AccessTokenResponse {
  return { accessToken: access, refreshToken: refresh, expiresIn: 300 };
}

const unauthorized = { status: 401, statusText: 'Unauthorized' };

describe('BearerAuthStrategy', () => {
  let native: FakeAndroidPlugins;
  let http: HttpTestingController;

  function start(): BearerAuthStrategy {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: API },
        { provide: ANDROID_PLUGINS, useValue: native.plugins },
        { provide: AUTH_STRATEGY, useExisting: BearerAuthStrategy },
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(BearerAuthStrategy);
  }

  function next(url: string): Promise<TestRequest> {
    return vi.waitFor(() => http.expectOne(url));
  }

  function get(url: string): Promise<unknown> {
    return firstValueFrom(TestBed.inject(HttpClient).get(url));
  }

  beforeEach(() => {
    native = new FakeAndroidPlugins();
  });

  afterEach(() => http.verify());

  it('answers an anonymous session without asking the API when it holds no token', async () => {
    const strategy = start();

    await expect(firstValueFrom(strategy.readSession())).resolves.toEqual({ user: null });
  });

  it('signs in for tokens, then reads the session with the access token', async () => {
    const strategy = start();

    const session = firstValueFrom(
      strategy.signIn({ identifier: 'Faker', password: 'secret', rememberMe: true }),
    );
    const token = await next(TOKEN_URL);
    expect(token.request.body).toEqual({ identifier: 'Faker', password: 'secret' });
    expect(token.request.headers.has('Authorization')).toBe(false);
    token.flush(tokens('access-1', 'refresh-1'));
    const me = await next(ME_URL);
    expect(me.request.headers.get('Authorization')).toBe('Bearer access-1');
    me.flush({ user: accountUser() });

    await expect(session).resolves.toEqual({ user: accountUser() });
    expect(native.stored.get(STORED_KEY)).toBe('refresh-1');
  });

  it('forgets the refresh token at exit unless the user asked to be remembered', async () => {
    const strategy = start();

    const session = firstValueFrom(strategy.signIn({ identifier: 'Faker', password: 'secret' }));
    (await next(TOKEN_URL)).flush(tokens('access-1', 'refresh-1'));
    (await next(ME_URL)).flush({ user: accountUser() });

    await session;
    expect(native.stored.has(STORED_KEY)).toBe(false);
  });

  it('passes a refused password on as it is, without renewing anything', async () => {
    const strategy = start();
    await TestBed.inject(BearerTokens).adopt(tokens('access-1', 'refresh-1'), true);

    const session = firstValueFrom(strategy.signIn({ identifier: 'Faker', password: 'wrong' }));
    (await next(TOKEN_URL)).flush({ code: 'invalid-credentials' }, unauthorized);

    await expect(session).rejects.toMatchObject({ status: 401 });
  });

  it('renews an access token the API refused, then sends the request once more', async () => {
    start();
    await TestBed.inject(BearerTokens).adopt(tokens('access-1', 'refresh-1'), true);

    const builds = get(BUILDS_URL);
    (await next(BUILDS_URL)).flush(null, unauthorized);
    (await next(REFRESH_URL)).flush(tokens('access-2', 'refresh-2'));
    const retried = await next(BUILDS_URL);
    expect(retried.request.headers.get('Authorization')).toBe('Bearer access-2');
    retried.flush(['build']);

    await expect(builds).resolves.toEqual(['build']);
  });

  it('gives up after one renewal: a second 401 is the answer', async () => {
    start();
    await TestBed.inject(BearerTokens).adopt(tokens('access-1', 'refresh-1'), true);

    const builds = get(BUILDS_URL);
    (await next(BUILDS_URL)).flush(null, unauthorized);
    (await next(REFRESH_URL)).flush(tokens('access-2', 'refresh-2'));
    (await next(BUILDS_URL)).flush(null, unauthorized);

    await expect(builds).rejects.toMatchObject({ status: 401 });
  });

  it('answers the 401 when the session cannot be renewed', async () => {
    start();
    await TestBed.inject(BearerTokens).adopt(tokens('access-1', 'refresh-1'), true);

    const builds = get(BUILDS_URL);
    (await next(BUILDS_URL)).flush(null, unauthorized);
    (await next(REFRESH_URL)).flush({ code: 'invalid-refresh-token' }, unauthorized);

    await expect(builds).rejects.toMatchObject({ status: 401 });
    expect(native.stored.has(STORED_KEY)).toBe(false);
  });

  it('reads the session anonymously once the stored refresh token is refused', async () => {
    native.stored.set(STORED_KEY, 'revoked');
    const strategy = start();

    const session = firstValueFrom(strategy.readSession());
    (await next(REFRESH_URL)).flush({ code: 'invalid-refresh-token' }, unauthorized);
    const me = await next(ME_URL);
    expect(me.request.headers.has('Authorization')).toBe(false);
    me.flush({ user: null });

    await expect(session).resolves.toEqual({ user: null });
  });

  it('signs out by forgetting the tokens', async () => {
    const strategy = start();
    await TestBed.inject(BearerTokens).adopt(tokens('access-1', 'refresh-1'), true);

    await firstValueFrom(strategy.signOut(), { defaultValue: undefined });

    expect(native.stored.has(STORED_KEY)).toBe(false);
    await expect(firstValueFrom(strategy.readSession())).resolves.toEqual({ user: null });
  });

  it('lands on a return URL of the app only', () => {
    const strategy = start();
    const origin = document.location.origin;

    expect(strategy.landingUrl(`${origin}/fr/builds?page=2`, '/fr')).toBe('/fr/builds?page=2');
    expect(strategy.landingUrl('https://evil.example/', '/fr')).toBe('/fr');
    expect(strategy.landingUrl('//evil.example/', '/fr')).toBe('/fr');
  });
});
