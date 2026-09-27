import { HttpErrorResponse, HttpStatusCode } from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { Component, PLATFORM_ID, RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type ActivatedRouteSnapshot, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import type { AccountUser } from '../../../core/api/generated/models/account-user';
import { AUTH_STRATEGY } from '../../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../core/auth/testing/fake-auth-strategy';
import { isOutcomeNavigation } from '../../../core/routing/outcome/is-outcome-navigation';
import { NOT_FOUND } from '../../../core/routing/outcome/not-found';
import { resolveOutcome } from '../../../core/routing/outcome/resolve-outcome';
import { adminAccessGuard } from './admin-access-guard';
import { adminAccessOf } from './admin-access';

@Component({ template: '' })
class Probe {}

const OPEN = accountUser({ roles: ['Admin'], multiFactor: true, twoFactorEnabled: true });
const SECOND_FACTOR = accountUser({ roles: ['Admin'], twoFactorEnabled: true });
const ENROLL = accountUser({ roles: ['Admin'] });
const MEMBER = accountUser({ multiFactor: true, twoFactorEnabled: true });

function setUp(strategy: FakeAuthStrategy, platform = 'browser'): Router {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        {
          path: '**',
          canMatch: [isOutcomeNavigation],
          resolve: { outcome: resolveOutcome },
          component: Probe,
        },
        {
          path: 'admin',
          children: [
            {
              path: 'login',
              canActivate: [adminAccessGuard('sign-in', 'second-factor', 'refused')],
              component: Probe,
            },
            { path: 'enroll', canActivate: [adminAccessGuard('enroll')], component: Probe },
            {
              path: '',
              canActivate: [adminAccessGuard('open')],
              canActivateChild: [adminAccessGuard('open')],
              component: Probe,
              children: [{ path: 'users', component: Probe }],
            },
          ],
        },
      ]),
      provideLocationMocks(),
      { provide: AUTH_STRATEGY, useValue: strategy },
      { provide: PLATFORM_ID, useValue: platform },
      { provide: RESPONSE_INIT, useValue: null },
    ],
  });
  return TestBed.inject(Router);
}

function signedIn(user: AccountUser | null): Router {
  const strategy = new FakeAuthStrategy();
  strategy.sessionAnswer = of({ user });
  return setUp(strategy);
}

function leafOf(router: Router): ActivatedRouteSnapshot {
  let route = router.routerState.snapshot.root;
  while (route.firstChild) {
    route = route.firstChild;
  }
  return route;
}

describe('adminAccessOf', () => {
  it.each([
    ['nobody', null, 'sign-in'],
    ['an account without the Admin role', MEMBER, 'refused'],
    ['an administrator signed in with the second factor', OPEN, 'open'],
    ['an administrator signed in with the password alone', SECOND_FACTOR, 'second-factor'],
    ['an administrator without an authenticator', ENROLL, 'enroll'],
  ])('ranks %s', (_who, user, access) => {
    expect(adminAccessOf(user)).toBe(access);
  });
});

describe('adminAccessGuard', () => {
  it('opens the admin to an administrator signed in with the second factor', async () => {
    const router = signedIn(OPEN);

    await router.navigateByUrl('/admin/users?page=2');

    expect(router.url).toBe('/admin/users?page=2');
  });

  it.each([
    ['a visitor', null],
    ['an administrator who skipped the second factor', SECOND_FACTOR],
  ])('sends %s to the admin login page, which brings them back', async (_who, user) => {
    const router = signedIn(user);

    await router.navigateByUrl('/admin/users?page=2');

    expect(router.url).toBe('/admin/login?returnUrl=%2Fadmin%2Fusers%3Fpage%3D2');
  });

  it('sends an administrator without an authenticator to its enrolment', async () => {
    const router = signedIn(ENROLL);

    await router.navigateByUrl('/admin');

    expect(router.url).toBe('/admin/enroll');
  });

  it('answers the 404 of the URL to an account that is no administrator', async () => {
    const router = signedIn(MEMBER);

    await router.navigateByUrl('/admin/users');

    expect(router.url).toBe('/admin/users');
    expect(leafOf(router).data['outcome']).toEqual(NOT_FOUND);
  });

  it('keeps the login and the enrolment from an administrator already in', async () => {
    const router = signedIn(OPEN);

    await router.navigateByUrl('/admin/login');
    expect(router.url).toBe('/admin');

    await router.navigateByUrl('/admin/enroll');
    expect(router.url).toBe('/admin');
  });

  it('keeps the enrolment from anyone who has nothing to enrol', async () => {
    const router = signedIn(null);

    await router.navigateByUrl('/admin/enroll');

    expect(router.url).toBe('/admin/login?returnUrl=%2Fadmin%2Fenroll');
  });

  it('renders the failure of the page when the session cannot be read', async () => {
    const strategy = new FakeAuthStrategy();
    const unavailable = new HttpErrorResponse({ status: HttpStatusCode.ServiceUnavailable });
    strategy.sessionAnswer = throwError(() => unavailable);
    const router = setUp(strategy);

    await router.navigateByUrl('/admin/users');

    expect(router.url).toBe('/admin/users');
    expect(leafOf(router).data['outcome']).toMatchObject({
      kind: 'failure',
      status: HttpStatusCode.ServiceUnavailable,
    });
  });

  it('lets the shell through on the server, where no session is read', async () => {
    const strategy = new FakeAuthStrategy();
    const router = setUp(strategy, 'server');

    await router.navigateByUrl('/admin');

    expect(router.url).toBe('/admin');
    expect(strategy.reads).toHaveLength(0);
  });
});
