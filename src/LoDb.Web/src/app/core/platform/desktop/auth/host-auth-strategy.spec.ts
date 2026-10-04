import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../../api/api-base-url';
import { authInterceptor } from '../../../auth/auth-interceptor';
import { AUTH_STRATEGY } from '../../../auth/strategy/auth-strategy-token';
import { accountUser } from '../../../auth/testing/account-user';
import { HostAuthStrategy } from './host-auth-strategy';

// The host serves the pages, its token endpoints and the relayed API on one loopback origin.
const ORIGIN = location.origin;
const ME_URL = `${ORIGIN}/api/account/me`;

describe('HostAuthStrategy', () => {
  let strategy: HostAuthStrategy;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: ORIGIN },
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: AUTH_STRATEGY, useExisting: HostAuthStrategy },
      ],
    });
    strategy = TestBed.inject(HostAuthStrategy);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('reads the session through the host, which adds the token itself', async () => {
    const session = firstValueFrom(strategy.readSession());

    const me = http.expectOne(ME_URL);
    expect(me.request.headers.has('Authorization')).toBe(false);
    me.flush({ user: accountUser() });

    await expect(session).resolves.toEqual({ user: accountUser() });
  });

  it('signs in with the host, then reads the session it opened', async () => {
    const credentials = { identifier: 'jinx', password: 'secret', rememberMe: true };
    const session = firstValueFrom(strategy.signIn(credentials));

    const login = http.expectOne({ method: 'POST', url: `${ORIGIN}/desktop/auth/login` });
    expect(login.request.body).toEqual(credentials);
    login.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne(ME_URL).flush({ user: accountUser() });

    await expect(session).resolves.toEqual({ user: accountUser() });
  });

  it('errors with the problem the host relayed from the API', async () => {
    const session = firstValueFrom(strategy.signIn({ identifier: 'jinx', password: 'nope' }));

    http
      .expectOne(`${ORIGIN}/desktop/auth/login`)
      .flush({ code: 'invalid-credentials' }, { status: 401, statusText: 'Unauthorized' });

    const error: unknown = await session.catch((failure: unknown) => failure);
    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect((error as HttpErrorResponse).error).toEqual({ code: 'invalid-credentials' });
  });

  it('signs out with the host, which forgets the tokens', async () => {
    const done = firstValueFrom(strategy.signOut());

    http
      .expectOne({ method: 'POST', url: `${ORIGIN}/desktop/auth/logout` })
      .flush(null, { status: 204, statusText: 'No Content' });

    await expect(done).resolves.toBeUndefined();
  });

  it('lands on a return URL only when it stays in the application', () => {
    expect(strategy.landingUrl('/fr/builds?page=2', '/fr/account/profile')).toBe(
      '/fr/builds?page=2',
    );
    expect(strategy.landingUrl('https://evil.example/', '/fr/account/profile')).toBe(
      '/fr/account/profile',
    );
  });
});
