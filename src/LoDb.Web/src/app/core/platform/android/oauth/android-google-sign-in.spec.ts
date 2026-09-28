import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  type TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Component, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { API_BASE_URL } from '../../../api/api-base-url';
import { authInterceptor } from '../../../auth/auth-interceptor';
import { AuthSession } from '../../../auth/session/auth-session';
import { AUTH_STRATEGY } from '../../../auth/strategy/auth-strategy-token';
import type { GoogleSignIn } from '../../../auth/strategy/google-sign-in';
import { accountUser } from '../../../auth/testing/account-user';
import { BearerAuthStrategy } from '../auth/bearer-auth-strategy';
import { ANDROID_PLUGINS } from '../native/android-plugins-token';
import { FakeAndroidPlugins } from '../testing/fake-android-plugins';
import { ANDROID_GOOGLE_CLIENT } from './android-google-client-token';
import { AndroidGoogleSignIn } from './android-google-sign-in';
import { GoogleReturnListener } from './google-return-listener';
import type { PendingGoogleSignIn } from './pending-google-sign-in';
import { pkceChallenge } from './pkce-challenge';

const API = 'https://league-of-data-base.com';
const EXCHANGE_URL = `${API}/api/account/google/app/exchange`;
const ME_URL = `${API}/api/account/me`;
const REDIRECT = `${API}/app/oauth/google`;
const PENDING_KEY = 'lodb.google-sign-in';
const REFRESH_KEY = 'lodb.refresh-token';
const LANDING = '/fr/builds';

const request: GoogleSignIn = {
  returnUrl: LANDING,
  fallbackUrl: '/fr/account/profile',
  locale: 'fr',
  rememberMe: true,
};

@Component({ template: '' })
class Page {}

describe('AndroidGoogleSignIn', () => {
  let native: FakeAndroidPlugins;
  let http: HttpTestingController;

  async function start(clientId: string | null = 'client'): Promise<AndroidGoogleSignIn> {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: Page }]),
        { provide: API_BASE_URL, useValue: API },
        { provide: ANDROID_PLUGINS, useValue: native.plugins },
        { provide: ANDROID_GOOGLE_CLIENT, useValue: { clientId, redirectUri: REDIRECT } },
        { provide: AUTH_STRATEGY, useExisting: BearerAuthStrategy },
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    await TestBed.inject(Router).navigateByUrl('/fr/account/login');
    return TestBed.inject(AndroidGoogleSignIn);
  }

  function pending(): PendingGoogleSignIn {
    return JSON.parse(native.stored.get(PENDING_KEY) ?? 'null') as PendingGoogleSignIn;
  }

  function exchange(): Promise<TestRequest> {
    return vi.waitFor(() => http.expectOne({ method: 'POST', url: EXCHANGE_URL }));
  }

  function currentUrl(): string {
    return TestBed.inject(Router).url;
  }

  beforeEach(() => {
    native = new FakeAndroidPlugins();
  });

  afterEach(() => http.verify());

  it('opens Google in the system browser, with the challenge of a verifier it keeps', async () => {
    const google = await start();

    await google.start(request, LANDING);

    const flow = pending();
    const opened = new URL(native.opened[0] ?? '');
    expect(opened.origin).toBe('https://accounts.google.com');
    expect(opened.searchParams.get('redirect_uri')).toBe(REDIRECT);
    expect(opened.searchParams.get('state')).toBe(flow.state);
    expect(opened.searchParams.get('code_challenge')).toBe(await pkceChallenge(flow.verifier));
    expect(flow).toMatchObject({ landingUrl: LANDING, locale: 'fr', isRemembered: true });
  });

  it('redeems the code the App Link brings back, then lands on the page asked', async () => {
    const google = await start();
    await google.start(request, LANDING);
    const { state, verifier } = pending();

    const completion = google.complete(`${REDIRECT}?code=code-1&state=${state}`);
    const redeem = await exchange();
    expect(redeem.request.body).toEqual({
      clientId: 'client',
      code: 'code-1',
      codeVerifier: verifier,
      redirectUri: REDIRECT,
    });
    expect(redeem.request.headers.has('Authorization')).toBe(false);
    redeem.flush({ accessToken: 'access-1', refreshToken: 'refresh-1', expiresIn: 300 });
    const me = await vi.waitFor(() => http.expectOne(ME_URL));
    expect(me.request.headers.get('Authorization')).toBe('Bearer access-1');
    me.flush({ user: accountUser() });
    await completion;

    expect(native.browserCloses).toBe(1);
    expect(native.stored.get(REFRESH_KEY)).toBe('refresh-1');
    expect(TestBed.inject(AuthSession).isAuthenticated()).toBe(true);
    expect(currentUrl()).toBe(LANDING);
  });

  it('completes a return that reaches the app while it runs', async () => {
    const google = await start();
    TestBed.inject(GoogleReturnListener).listen();
    await google.start(request, LANDING);

    native.emit('appUrlOpen', { url: `${REDIRECT}?code=code-1&state=${pending().state}` });

    (await exchange()).flush({ code: 'google-failed' }, { status: 401, statusText: '' });
    await vi.waitFor(() => expect(currentUrl()).toBe('/fr/account/login?error=google-failed'));
  });

  it('completes a return that started the app cold', async () => {
    const google = await start();
    await google.start(request, LANDING);
    native.launchUrl = `${REDIRECT}?code=code-1&state=${pending().state}`;

    TestBed.inject(GoogleReturnListener).listen();

    (await exchange()).flush({ code: 'account-banned' }, { status: 403, statusText: '' });
    await vi.waitFor(() => expect(currentUrl()).toBe('/fr/account/login?error=account-banned'));
  });

  it('signs no one in with a state this app did not send', async () => {
    const google = await start();
    await google.start(request, LANDING);

    await google.complete(`${REDIRECT}?code=forged&state=other`);

    expect(currentUrl()).toBe('/fr/account/login?error=google-failed');
    expect(native.stored.has(PENDING_KEY)).toBe(false);
  });

  it('signs no one in when no sign-in is pending', async () => {
    const google = await start();

    await google.complete(`${REDIRECT}?code=code-1&state=state-1`);

    expect(currentUrl()).toBe('/en/account/login?error=google-failed');
  });

  it('tells a declined consent from a failure', async () => {
    const google = await start();
    await google.start(request, LANDING);

    await google.complete(`${REDIRECT}?error=access_denied&state=${pending().state}`);

    expect(currentUrl()).toBe('/fr/account/login?error=google-cancelled');
  });

  it('shows the code the API refused the exchange with', async () => {
    const google = await start();
    await google.start(request, LANDING);

    const completion = google.complete(`${REDIRECT}?code=code-1&state=${pending().state}`);
    const answer = { code: 'google-email-unverified' };
    (await exchange()).flush(answer, { status: 403, statusText: '' });
    await completion;

    expect(currentUrl()).toBe('/fr/account/login?error=google-email-unverified');
    expect(native.stored.has(REFRESH_KEY)).toBe(false);
  });

  it('leaves any other link alone', async () => {
    const google = await start();

    await google.complete(`${API}/fr/builds?code=code-1`);

    expect(native.browserCloses).toBe(0);
    expect(currentUrl()).toBe('/fr/account/login');
  });

  it('says Google is unavailable when the build names no Google client', async () => {
    const google = await start(null);

    await google.start(request, LANDING);

    expect(native.opened).toEqual([]);
    expect(currentUrl()).toBe('/fr/account/login?error=google-unavailable');
  });
});

describe('ANDROID_GOOGLE_CLIENT', () => {
  it('redirects to the App Link of the site, and names no client without one in the build', () => {
    const client = TestBed.inject(ANDROID_GOOGLE_CLIENT);

    // The web environment of the specs has no public API origin: the page's stands in.
    expect(client).toEqual({
      clientId: null,
      redirectUri: `${document.location.origin}/app/oauth/google`,
    });
  });
});
