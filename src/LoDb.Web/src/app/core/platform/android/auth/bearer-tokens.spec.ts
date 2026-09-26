import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  type TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../../api/api-base-url';
import type { AccessTokenResponse } from '../../../api/generated/models/access-token-response';
import { AuthSession } from '../../../auth/session/auth-session';
import { AUTH_STRATEGY } from '../../../auth/strategy/auth-strategy-token';
import { FakeAuthStrategy } from '../../../auth/testing/fake-auth-strategy';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { FakeAndroidPlugins } from '../testing/fake-android-plugins';
import { BearerTokens } from './bearer-tokens';

const API = 'https://league-of-data-base.com';
const REFRESH_URL = `${API}/api/account/refresh`;
const STORED_KEY = 'lodb.refresh-token';
const START = Date.UTC(2026, 8, 26, 12);
const FIVE_MINUTES_S = 300;

function tokens(access: string, refresh: string): AccessTokenResponse {
  return { accessToken: access, refreshToken: refresh, expiresIn: FIVE_MINUTES_S };
}

describe('BearerTokens', () => {
  let native: FakeAndroidPlugins;
  let http: HttpTestingController;
  let now: number;

  function start(): BearerTokens {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: API },
        { provide: ANDROID_PLUGINS, useValue: native.plugins },
        { provide: AUTH_STRATEGY, useValue: new FakeAuthStrategy() },
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(BearerTokens);
  }

  // The renewal leaves after the secure storage answered: wait for it rather than guess.
  function renewal(): Promise<TestRequest> {
    return vi.waitFor(() => http.expectOne({ method: 'POST', url: REFRESH_URL }));
  }

  beforeEach(() => {
    native = new FakeAndroidPlugins();
    now = START;
    vi.spyOn(Date, 'now').mockImplementation(() => now);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  it('holds no session on a first run, and asks nothing to know it', async () => {
    const holder = start();

    await expect(holder.hasSession()).resolves.toBe(false);
    await expect(holder.accessToken()).resolves.toBeNull();
  });

  it('renews from the refresh token an earlier run kept, and keeps the rotated one', async () => {
    native.stored.set(STORED_KEY, 'refresh-1');
    const holder = start();

    const token = holder.accessToken();
    const request = await renewal();
    expect(request.request.body).toEqual({ refreshToken: 'refresh-1' });
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush(tokens('access-2', 'refresh-2'));

    await expect(token).resolves.toBe('access-2');
    expect(native.stored.get(STORED_KEY)).toBe('refresh-2');
  });

  it('answers a live access token from memory', async () => {
    const holder = start();
    await holder.adopt(tokens('access-1', 'refresh-1'), true);

    now += 60_000;

    await expect(holder.accessToken()).resolves.toBe('access-1');
  });

  it('renews 30 seconds before the access token expires', async () => {
    const holder = start();
    await holder.adopt(tokens('access-1', 'refresh-1'), true);

    now += (FIVE_MINUTES_S - 30) * 1000;
    const token = holder.accessToken();
    (await renewal()).flush(tokens('access-2', 'refresh-2'));

    await expect(token).resolves.toBe('access-2');
  });

  it('sends one renewal however many requests find the token expired', async () => {
    native.stored.set(STORED_KEY, 'refresh-1');
    const holder = start();

    const first = holder.accessToken();
    const second = holder.accessToken();
    (await renewal()).flush(tokens('access-2', 'refresh-2'));

    await expect(Promise.all([first, second])).resolves.toEqual(['access-2', 'access-2']);
  });

  it('keeps the refresh token in memory only when the user did not ask to be remembered', async () => {
    native.stored.set(STORED_KEY, 'refresh-of-someone-else');
    const holder = start();
    await holder.adopt(tokens('access-1', 'refresh-1'), false);
    expect(native.stored.has(STORED_KEY)).toBe(false);

    now += FIVE_MINUTES_S * 1000;
    const token = holder.accessToken();
    const request = await renewal();
    expect(request.request.body).toEqual({ refreshToken: 'refresh-1' });
    request.flush(tokens('access-2', 'refresh-2'));

    await expect(token).resolves.toBe('access-2');
    expect(native.stored.has(STORED_KEY)).toBe(false);
  });

  it('signs out everywhere when the API refuses the refresh token', async () => {
    native.stored.set(STORED_KEY, 'revoked');
    const holder = start();

    const token = holder.accessToken();
    (await renewal()).flush({ code: 'invalid-refresh-token' }, { status: 401, statusText: '' });

    await expect(token).resolves.toBeNull();
    expect(native.stored.has(STORED_KEY)).toBe(false);
    await expect(holder.hasSession()).resolves.toBe(false);
    expect(TestBed.inject(AuthSession).status()).toBe('anonymous');
  });

  it('keeps the session when the API cannot be reached, and tries again later', async () => {
    native.stored.set(STORED_KEY, 'refresh-1');
    const holder = start();

    const token = holder.accessToken();
    (await renewal()).error(new ProgressEvent('error'));

    await expect(token).rejects.toMatchObject({ status: 0 });
    expect(native.stored.get(STORED_KEY)).toBe('refresh-1');
    const retry = holder.accessToken();
    (await renewal()).flush(tokens('access-2', 'refresh-2'));
    await expect(retry).resolves.toBe('access-2');
  });

  it('drops a renewal answered after the user signed out', async () => {
    native.stored.set(STORED_KEY, 'refresh-1');
    const holder = start();

    const token = holder.accessToken();
    const request = await renewal();
    await holder.clear();
    request.flush(tokens('access-2', 'refresh-2'));

    await expect(token).resolves.toBeNull();
    expect(native.stored.has(STORED_KEY)).toBe(false);
  });

  it('hands a 401 the token a concurrent renewal already brought', async () => {
    const holder = start();
    await holder.adopt(tokens('access-2', 'refresh-2'), true);

    await expect(holder.replace('access-1')).resolves.toBe('access-2');
  });

  it('renews on a 401 answered to the current token', async () => {
    const holder = start();
    await holder.adopt(tokens('access-1', 'refresh-1'), true);

    const token = holder.replace('access-1');
    (await renewal()).flush(tokens('access-2', 'refresh-2'));

    await expect(token).resolves.toBe('access-2');
  });

  it('works for the life of the process when the Keystore fails', async () => {
    native.storageFailure = new Error('Keystore unavailable');
    const holder = start();

    await holder.adopt(tokens('access-1', 'refresh-1'), true);

    await expect(holder.accessToken()).resolves.toBe('access-1');
  });
});
