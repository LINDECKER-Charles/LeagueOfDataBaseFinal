import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MOCK_PLATFORM_LOCATION_CONFIG } from '@angular/common/testing';
import { DOCUMENT, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../api/api-base-url';
import { authInterceptor } from '../auth-interceptor';
import { AUTH_STRATEGY } from '../strategy/auth-strategy-token';
import { accountUser } from '../testing/account-user';
import { CookieAuthStrategy } from './cookie-auth-strategy';

// The web API is served by the page's own origin, which is what lets the cookie and the XSRF
// token of `HttpClient` apply.
const ORIGIN = 'https://leagueofdatabase.com';
const XSRF_COOKIE = 'XSRF-TOKEN';

interface Setup {
  readonly strategy: CookieAuthStrategy;
  readonly http: HttpTestingController;
}

function setUp(): Setup {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: ORIGIN },
      {
        provide: MOCK_PLATFORM_LOCATION_CONFIG,
        useValue: { startUrl: `${ORIGIN}/fr/account/login` },
      },
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: AUTH_STRATEGY, useExisting: CookieAuthStrategy },
    ],
  });
  return {
    strategy: TestBed.inject(CookieAuthStrategy),
    http: TestBed.inject(HttpTestingController),
  };
}

// A document whose navigation the spec records, on another origin than the API's.
function setUpNavigation(pageOrigin: string): { strategy: CookieAuthStrategy; opened: string[] } {
  const opened: string[] = [];
  const location = { origin: pageOrigin, assign: (url: string) => opened.push(url) };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      { provide: API_BASE_URL, useValue: 'https://api.example' },
      { provide: DOCUMENT, useValue: { location } },
    ],
  });
  return { strategy: TestBed.inject(CookieAuthStrategy), opened };
}

describe('CookieAuthStrategy', () => {
  afterEach(() => {
    document.cookie = `${XSRF_COOKIE}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
  });

  it('reads the session on /api/account/me', async () => {
    const { strategy, http } = setUp();

    const session = firstValueFrom(strategy.readSession());
    http.expectOne({ method: 'GET', url: `${ORIGIN}/api/account/me` }).flush({ user: null });

    await expect(session).resolves.toEqual({ user: null });
    http.verify();
  });

  it('signs in with the XSRF token HttpClient copies from its cookie', async () => {
    const { strategy, http } = setUp();
    document.cookie = `${XSRF_COOKIE}=token-from-the-api; path=/`;
    const credentials = { identifier: 'Faker', password: 'secret', rememberMe: true };

    const session = firstValueFrom(strategy.signIn(credentials));
    const request = http.expectOne({ method: 'POST', url: `${ORIGIN}/api/account/login` });
    request.flush({ user: accountUser() });

    expect(request.request.body).toEqual(credentials);
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('token-from-the-api');
    expect(request.request.headers.has('Authorization')).toBe(false);
    await expect(session).resolves.toEqual({ user: accountUser() });
  });

  it('signs out with the XSRF token too', async () => {
    const { strategy, http } = setUp();
    document.cookie = `${XSRF_COOKIE}=another-token; path=/`;

    const done = firstValueFrom(strategy.signOut(), { defaultValue: undefined });
    const request = http.expectOne({ method: 'POST', url: `${ORIGIN}/api/account/logout` });
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('another-token');
    await expect(done).resolves.toBeUndefined();
  });

  it('lets the API requests leave as they are: the browser sends the cookie itself', () => {
    const { http } = setUp();

    TestBed.inject(CookieAuthStrategy).readSession().subscribe();

    const request = http.expectOne(`${ORIGIN}/api/account/me`).request;
    expect(request.headers.keys()).toEqual(['Accept']);
    expect(request.withCredentials).toBe(false);
  });

  it.each([
    ['/fr/account/builds?page=2', '/fr/account/builds?page=2'],
    ['https://app.example/en/items', '/en/items'],
    ['https://evil.example/fr/', '/fr/account/profile'],
    ['//evil.example/fr/', '/fr/account/profile'],
    [null, '/fr/account/profile'],
  ])('lands a sign-in asked back to %j on %j', (returnUrl, expected) => {
    const { strategy } = setUpNavigation('https://app.example');

    expect(strategy.landingUrl(returnUrl, '/fr/account/profile')).toBe(expected);
  });

  it('opens the Google sign-in of the API as a page, back to a safe URL', async () => {
    const { strategy, opened } = setUpNavigation('https://app.example');

    await strategy.startGoogleSignIn({
      returnUrl: 'https://evil.example/',
      fallbackUrl: '/fr/account/profile',
      locale: 'fr',
      rememberMe: false,
    });

    expect(opened).toEqual([
      'https://api.example/api/account/google/start?ReturnUrl=%2Ffr%2Faccount%2Fprofile&Locale=fr&RememberMe=false',
    ]);
  });
});
