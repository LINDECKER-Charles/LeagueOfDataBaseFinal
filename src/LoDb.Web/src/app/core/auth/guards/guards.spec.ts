import { HttpErrorResponse, HttpHeaders, HttpStatusCode } from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { Component, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type ActivatedRouteSnapshot, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import type { AccountUser } from '../../api/generated/models/account-user';
import { isOutcomeNavigation } from '../../routing/outcome/is-outcome-navigation';
import { NOT_FOUND } from '../../routing/outcome/not-found';
import { resolveOutcome } from '../../routing/outcome/resolve-outcome';
import { AUTH_STRATEGY } from '../strategy/auth-strategy-token';
import { accountUser } from '../testing/account-user';
import { FakeAuthStrategy } from '../testing/fake-auth-strategy';
import { adminGuard } from './admin-guard';
import { authenticatedGuard } from './authenticated-guard';
import { verifiedEmailGuard } from './verified-email-guard';

@Component({ template: '' })
class Probe {}

const ADMIN = accountUser({ roles: ['Admin'], multiFactor: true });

function setUp(user: AccountUser | null): Router {
  const strategy = new FakeAuthStrategy();
  strategy.sessionAnswer = of({ user });
  return setUpWith(strategy);
}

function setUpWith(strategy: FakeAuthStrategy): Router {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        {
          path: '**',
          canMatch: [isOutcomeNavigation],
          resolve: { outcome: resolveOutcome },
          component: Probe,
        },
        { path: 'admin', canActivate: [adminGuard], component: Probe },
        {
          path: ':locale',
          children: [
            { path: 'account/login', component: Probe },
            { path: 'account/verify-email', component: Probe },
            { path: 'account/builds', canActivate: [authenticatedGuard], component: Probe },
            { path: 'builds/new', canActivate: [verifiedEmailGuard], component: Probe },
          ],
        },
      ]),
      provideLocationMocks(),
      { provide: AUTH_STRATEGY, useValue: strategy },
      { provide: PLATFORM_ID, useValue: 'browser' },
      { provide: RESPONSE_INIT, useValue: null },
    ],
  });
  return TestBed.inject(Router);
}

function leafOf(router: Router): ActivatedRouteSnapshot {
  let route = router.routerState.snapshot.root;
  while (route.firstChild) {
    route = route.firstChild;
  }
  return route;
}

describe('authenticatedGuard', () => {
  it('lets a signed-in account through', async () => {
    const router = setUp(accountUser());

    await router.navigateByUrl('/fr/account/builds');

    expect(router.url).toBe('/fr/account/builds');
  });

  it('sends anyone else to the login page of the locale, which brings them back', async () => {
    const router = setUp(null);

    await router.navigateByUrl('/fr/account/builds?page=2');

    expect(router.url).toBe('/fr/account/login?returnUrl=%2Ffr%2Faccount%2Fbuilds%3Fpage%3D2');
  });

  it('waits for the session a first read is fetching', async () => {
    const strategy = new FakeAuthStrategy();
    const router = setUpWith(strategy);

    const navigation = router.navigateByUrl('/en/account/builds');
    await vi.waitFor(() => expect(strategy.reads).toHaveLength(1));
    strategy.answerRead(accountUser());

    await expect(navigation).resolves.toBe(true);
    expect(router.url).toBe('/en/account/builds');
  });

  it('renders the 503 of the page when the session cannot be read', async () => {
    const strategy = new FakeAuthStrategy();
    const headers = new HttpHeaders({ 'Retry-After': '30' });
    strategy.sessionAnswer = throwError(() => new HttpErrorResponse({ status: 503, headers }));
    const router = setUpWith(strategy);

    await router.navigateByUrl('/fr/account/builds');

    expect(router.url).toBe('/fr/account/builds');
    expect(leafOf(router).data['outcome']).toEqual({
      kind: 'failure',
      status: HttpStatusCode.ServiceUnavailable,
      retryAfter: '30',
    });
  });
});

describe('verifiedEmailGuard', () => {
  it('lets a verified account through', async () => {
    const router = setUp(accountUser());

    await router.navigateByUrl('/fr/builds/new');

    expect(router.url).toBe('/fr/builds/new');
  });

  it('sends an unverified account to the verification page', async () => {
    const router = setUp(accountUser({ emailVerified: false }));

    await router.navigateByUrl('/fr/builds/new');

    expect(router.url).toBe('/fr/account/verify-email?returnUrl=%2Ffr%2Fbuilds%2Fnew');
  });

  it('sends a visitor to the login page', async () => {
    const router = setUp(null);

    await router.navigateByUrl('/de/builds/new');

    expect(router.url).toBe('/de/account/login?returnUrl=%2Fde%2Fbuilds%2Fnew');
  });
});

describe('adminGuard', () => {
  it('lets an administrator signed in with a second factor through', async () => {
    const router = setUp(ADMIN);

    await router.navigateByUrl('/admin');

    expect(router.url).toBe('/admin');
    expect(leafOf(router).data).toEqual({});
  });

  it('sends a visitor to the login page of the default locale', async () => {
    const router = setUp(null);

    await router.navigateByUrl('/admin');

    expect(router.url).toBe('/en/account/login?returnUrl=%2Fadmin');
  });

  it.each([
    ['an account', accountUser()],
    ['an administrator without a second factor', accountUser({ roles: ['Admin'] })],
  ])('answers the 404 of the URL to %s', async (_who, user) => {
    const router = setUp(user);

    await router.navigateByUrl('/admin');

    expect(router.url).toBe('/admin');
    expect(leafOf(router).data['outcome']).toEqual(NOT_FOUND);
  });
});
