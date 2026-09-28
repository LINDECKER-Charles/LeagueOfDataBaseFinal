import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthSession } from '../../../core/auth/session/auth-session';
import { AUTH_STRATEGY } from '../../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../../core/auth/testing/fake-auth-strategy';
import { configureAdminTestBed } from '../testing/admin-test-bed';
import { openAdminPage } from '../testing/open-admin-page';
import { AdminFrame } from './admin-frame';

@Component({ template: '<p class="probe">page</p>' })
class Probe {}

const ADMIN = accountUser({ username: 'root', roles: ['Admin'], multiFactor: true });

function setUp(signedIn = true): FakeAuthStrategy {
  const strategy = new FakeAuthStrategy();
  configureAdminTestBed(
    [
      {
        path: 'admin',
        component: AdminFrame,
        children: [
          { path: 'login', component: Probe },
          { path: 'users', component: Probe },
          { path: 'users/:id/activity', component: Probe },
          { path: '', component: Probe },
        ],
      },
    ],
    [{ provide: AUTH_STRATEGY, useValue: strategy }],
  );
  TestBed.inject(AuthSession).apply({ user: signedIn ? ADMIN : null });
  return strategy;
}

// The link of the bar that says it is the current page.
function current(page: HTMLElement): string[] {
  return [...page.querySelectorAll('nav a[aria-current="page"]')].map(
    (link) => link.getAttribute('href') ?? '',
  );
}

describe('AdminFrame', () => {
  afterEach(() => (document.documentElement.lang = 'en'));

  it('shows its brand alone to a visitor, and speaks French', async () => {
    setUp(false);

    const { harness } = await openAdminPage('/admin/login');
    const frame = harness.fixture.nativeElement as HTMLElement;

    expect(frame.querySelector('header')?.textContent).toContain('admin.brand_name');
    expect(frame.querySelector('nav')).toBeNull();
    expect(frame.querySelector('.probe')).not.toBeNull();
    expect(document.documentElement.lang).toBe('fr');
  });

  it('lays out the two sections of the navigation, the current page marked', async () => {
    setUp();

    const { harness } = await openAdminPage('/admin/users?q=ahri');
    const frame = harness.fixture.nativeElement as HTMLElement;

    expect(frame.querySelectorAll('nav a')).toHaveLength(11);
    expect(frame.querySelectorAll('nav .admin-nav-sep')).toHaveLength(1);
    expect(current(frame)).toEqual(['/admin/users']);
  });

  it('marks the overview whatever its period, and the journal for an activity', async () => {
    setUp();

    const { harness } = await openAdminPage('/admin?range=7d');
    const frame = harness.fixture.nativeElement as HTMLElement;
    expect(current(frame)).toEqual(['/admin']);

    await TestBed.inject(Router).navigateByUrl('/admin/users/3/activity');
    harness.detectChanges();
    expect(current(frame)).toEqual(['/admin/journal']);
  });

  it('signs out, then goes back to the admin sign-in', async () => {
    const strategy = setUp();
    const signOut = vi.spyOn(strategy, 'signOut');
    const { harness } = await openAdminPage('/admin/users');
    const frame = harness.fixture.nativeElement as HTMLElement;

    frame.querySelector<HTMLButtonElement>('header button')?.click();
    await harness.fixture.whenStable();

    expect(signOut).toHaveBeenCalledTimes(1);
    expect(TestBed.inject(AuthSession).user()).toBeNull();
    expect(TestBed.inject(Router).url).toBe('/admin/login');
  });
});
