import { Component, PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type CanActivateFn, provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import type { AccountUser } from '../../../../core/api/generated/models/account-user';
import { AUTH_STRATEGY } from '../../../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../../core/auth/testing/fake-auth-strategy';
import { anonymousGuard } from './anonymous-guard';
import { browserOnly } from './browser-only';

@Component({ template: '' })
class Probe {}

function configure(platform: 'browser' | 'server', guard: CanActivateFn): FakeAuthStrategy {
  const strategy = new FakeAuthStrategy();
  TestBed.configureTestingModule({
    providers: [
      { provide: PLATFORM_ID, useValue: platform },
      { provide: AUTH_STRATEGY, useValue: strategy },
      provideRouter([
        {
          path: ':locale',
          children: [
            { path: 'account/login', canActivate: [guard], component: Probe },
            { path: '**', component: Probe },
          ],
        },
      ]),
    ],
  });
  return strategy;
}

function signedIn(strategy: FakeAuthStrategy, user: AccountUser | null): void {
  strategy.sessionAnswer = of({ user });
}

async function visit(url: string): Promise<string> {
  const router = TestBed.inject(Router);
  await router.navigateByUrl(url);
  return router.url;
}

describe('browserOnly', () => {
  it('lets every route through on the server, without asking the guard', async () => {
    const guard = vi.fn<CanActivateFn>(() => false);
    configure('server', browserOnly(guard));

    expect(await visit('/en/account/login')).toBe('/en/account/login');
    expect(guard).not.toHaveBeenCalled();
  });

  it('leaves the decision to the guard in the browser', async () => {
    const guard = vi.fn<CanActivateFn>(() => false);
    configure('browser', browserOnly(guard));

    expect(await visit('/en/account/login')).toBe('/');
    expect(guard).toHaveBeenCalledOnce();
  });
});

describe('anonymousGuard', () => {
  it('shows the form to a visitor', async () => {
    signedIn(configure('browser', anonymousGuard), null);

    expect(await visit('/fr/account/login')).toBe('/fr/account/login');
  });

  it('sends a signed-in account on to its profile, in the locale of the page', async () => {
    signedIn(configure('browser', anonymousGuard), accountUser());

    expect(await visit('/fr/account/login')).toBe('/fr/account/profile');
  });

  it('sends it to the page it was asked to return to, when that stays on the site', async () => {
    signedIn(configure('browser', anonymousGuard), accountUser());

    const back = encodeURIComponent('/fr/champions?lang=fr_FR');
    expect(await visit(`/fr/account/login?returnUrl=${back}`)).toBe('/fr/champions?lang=fr_FR');
  });

  it('never follows a return URL off the site', async () => {
    signedIn(configure('browser', anonymousGuard), accountUser());

    const away = encodeURIComponent('https://evil.example/phish');
    expect(await visit(`/en/account/login?returnUrl=${away}`)).toBe('/en/account/profile');
  });

  it('shows the form when the session cannot be read', async () => {
    const strategy = configure('browser', anonymousGuard);
    strategy.sessionAnswer = throwError(() => new Error('offline'));

    expect(await visit('/en/account/login')).toBe('/en/account/login');
  });
});
