import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthSession } from '../../core/auth/session/auth-session';
import { AUTH_STRATEGY } from '../../core/auth/strategy/auth-strategy-token';
import { accountUser } from '../../core/auth/testing/account-user';
import { FakeAuthStrategy } from '../../core/auth/testing/fake-auth-strategy';
import { AdminPage } from './admin-page';
import { analyticsReport } from './testing/fixtures/analytics-report';
import { monitoringReport } from './testing/fixtures/monitoring-report';
import { storageReport } from './testing/fixtures/storage-report';
import { configureAdminTestBed } from './testing/admin-test-bed';
import { openAdminPage } from './testing/open-admin-page';
import { sent } from './testing/http/sent';

@Component({ template: '<p class="probe">panel</p>' })
class Probe {}

const ADMIN = accountUser({ username: 'root', roles: ['Admin'], multiFactor: true });
const OVERVIEW_CALLS = [
  { path: '/api/admin/analytics/report', body: analyticsReport() },
  { path: '/api/admin/monitoring', body: monitoringReport() },
  { path: '/api/admin/storage', body: storageReport() },
] as const;

function setUp(): FakeAuthStrategy {
  const strategy = new FakeAuthStrategy();
  configureAdminTestBed(
    [
      { path: 'admin/login', component: Probe },
      { path: 'admin', component: AdminPage, children: [{ path: 'users', component: Probe }] },
    ],
    [{ provide: AUTH_STRATEGY, useValue: strategy }],
  );
  TestBed.inject(AuthSession).apply({ user: ADMIN });
  return strategy;
}

// The links of the admin's own navigation, the first `nav`, the current one marked.
function current(page: HTMLElement): string[] {
  const nav = page.querySelector('nav');
  return [...(nav?.querySelectorAll('a[aria-current="page"]') ?? [])].map(
    (link) => link.getAttribute('href') ?? '',
  );
}

describe('AdminPage', () => {
  it('lays out the navigation and shows the overview at /admin', async () => {
    setUp();

    const { page } = await openAdminPage('/admin', async (http) => {
      for (const call of OVERVIEW_CALLS) {
        (await sent({ http }, call.path)).flush(call.body);
      }
    });

    expect(page.querySelector('nav')?.textContent).toContain('admin.brand');
    expect(page.querySelector('nav')?.textContent).toContain('root');
    expect(page.querySelectorAll('nav li a')).toHaveLength(11);
    expect(current(page)).toEqual(['/admin']);
    expect(page.querySelector('lodb-overview-panel lodb-kpi')).not.toBeNull();
    expect(page.querySelector('.probe')).toBeNull();
  });

  it('shows the panel of the URL instead of the overview', async () => {
    setUp();

    const { page, http } = await openAdminPage('/admin/users');

    expect(page.querySelector('.probe')).not.toBeNull();
    expect(page.querySelector('lodb-overview-panel')).toBeNull();
    expect(current(page)).toEqual(['/admin/users']);
    http.expectNone(() => true);
  });

  it('signs out, then goes back to the admin sign-in', async () => {
    const strategy = setUp();
    const signOut = vi.spyOn(strategy, 'signOut');
    const { page, harness } = await openAdminPage('/admin/users');

    [...page.querySelectorAll('button')]
      .find((button) => button.textContent?.trim() === 'admin.nav.sign_out')
      ?.click();
    await harness.fixture.whenStable();

    expect(signOut).toHaveBeenCalledTimes(1);
    expect(TestBed.inject(AuthSession).user()).toBeNull();
    expect(TestBed.inject(Router).url).toBe('/admin/login');
  });
});
