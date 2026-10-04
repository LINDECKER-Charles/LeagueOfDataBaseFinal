import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { API_BASE_URL } from '../../../api/api-base-url';
import { AuthSession } from '../../../auth/session/auth-session';
import { AUTH_STRATEGY } from '../../../auth/strategy/auth-strategy-token';
import type { GoogleSignIn } from '../../../auth/strategy/google-sign-in';
import { accountUser } from '../../../auth/testing/account-user';
import { HostAuthStrategy } from './host-auth-strategy';
import { GOOGLE_DEADLINE_MS, GOOGLE_POLL_MS, HostGoogleSignIn } from './host-google-sign-in';

const ORIGIN = location.origin;
const BEGIN_URL = `${ORIGIN}/desktop/auth/google`;
const SESSION_URL = `${ORIGIN}/desktop/auth/session`;
const LANDING = '/fr/builds';

const request: GoogleSignIn = {
  returnUrl: LANDING,
  fallbackUrl: '/fr/account/profile',
  locale: 'fr',
  rememberMe: true,
};

@Component({ template: '' })
class Page {}

describe('HostGoogleSignIn', () => {
  let http: HttpTestingController;
  let google: HostGoogleSignIn;

  function currentUrl(): string {
    return TestBed.inject(Router).url;
  }

  // Starts the flow and lets the host accept it.
  async function begin(): Promise<void> {
    const started = google.start(request, LANDING);
    const opening = http.expectOne((r) => r.url === BEGIN_URL);
    expect(opening.request.params.get('rememberMe')).toBe('true');
    opening.flush(null, { status: 202, statusText: 'Accepted' });
    await started;
  }

  // The next poll, answered with the host's session.
  async function poll(session: object): Promise<void> {
    await vi.advanceTimersByTimeAsync(GOOGLE_POLL_MS);
    http.expectOne(SESSION_URL).flush(session);
    await vi.advanceTimersByTimeAsync(0);
  }

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: Page }]),
        { provide: API_BASE_URL, useValue: ORIGIN },
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: AUTH_STRATEGY, useExisting: HostAuthStrategy },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    await TestBed.inject(Router).navigateByUrl('/fr/account/login');
    google = TestBed.inject(HostGoogleSignIn);
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'Date'] });
  });

  afterEach(() => {
    vi.useRealTimers();
    http.verify();
  });

  it('polls the host until the browser sign-in ended, then lands signed in', async () => {
    await begin();
    await poll({ signedIn: false, remembered: false, google: 'pending' });

    await poll({ signedIn: true, remembered: true, google: 'idle' });
    http.expectOne(`${ORIGIN}/api/account/me`).flush({ user: accountUser() });
    await vi.advanceTimersByTimeAsync(0);

    expect(TestBed.inject(AuthSession).isAuthenticated()).toBe(true);
    expect(currentUrl()).toBe(LANDING);
  });

  it.each([
    ['cancelled', 'google-cancelled'],
    ['expired', 'google-failed'],
    ['browser-unavailable', 'google-failed'],
    ['account-banned', 'account-banned'],
    [null, 'google-failed'],
  ])('shows the login page with the reason of a %s failure', async (failure, code) => {
    await begin();

    await poll({ signedIn: false, google: 'failed', googleFailure: failure });

    expect(currentUrl()).toBe(`/fr/account/login?error=${code}`);
  });

  it('counts an idle host holding no session as a failure', async () => {
    await begin();

    await poll({ signedIn: false, google: 'idle' });

    expect(currentUrl()).toBe('/fr/account/login?error=google-failed');
  });

  it('keeps polling through unreadable answers and failed reads', async () => {
    await begin();
    await poll({ google: 'finished' });
    await vi.advanceTimersByTimeAsync(GOOGLE_POLL_MS);
    http.expectOne(SESSION_URL).flush(null, { status: 502, statusText: 'Bad Gateway' });
    await vi.advanceTimersByTimeAsync(0);

    await poll({ signedIn: false, google: 'failed', googleFailure: 'cancelled' });

    expect(currentUrl()).toBe('/fr/account/login?error=google-cancelled');
  });

  it('gives up once the host would have let the flow expire', async () => {
    await begin();

    for (let elapsed = 0; elapsed < GOOGLE_DEADLINE_MS; elapsed += GOOGLE_POLL_MS) {
      await poll({ signedIn: false, google: 'pending' });
    }

    expect(currentUrl()).toBe('/fr/account/login?error=google-failed');
  });

  it('stops following a flow a newer one replaced', async () => {
    await begin();
    await begin();

    await vi.advanceTimersByTimeAsync(GOOGLE_POLL_MS);

    // One poll: the first flow's timer ended without asking the host.
    http
      .expectOne(SESSION_URL)
      .flush({ signedIn: false, google: 'failed', googleFailure: 'expired' });
    await vi.advanceTimersByTimeAsync(0);
    expect(currentUrl()).toBe('/fr/account/login?error=google-failed');
  });

  it.each([
    [503, { code: 'google-unavailable' }, 'google-unavailable'],
    [500, { code: 'browser-unavailable' }, 'google-failed'],
    [502, 'Bad Gateway', 'google-failed'],
  ])('shows why the host could not open Google (%i)', async (status, body, code) => {
    const started = google.start(request, LANDING);
    http.expectOne((r) => r.url === BEGIN_URL).flush(body, { status, statusText: '' });
    await started;
    await vi.advanceTimersByTimeAsync(GOOGLE_POLL_MS);

    expect(currentUrl()).toBe(`/fr/account/login?error=${code}`);
    http.expectNone(SESSION_URL);
  });
});
