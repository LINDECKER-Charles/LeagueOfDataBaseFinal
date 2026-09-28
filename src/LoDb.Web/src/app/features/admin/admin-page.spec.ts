import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminPage } from './admin-page';
import { AdminNotices } from './layout/admin-notices';
import { PANEL_ROUTES } from './panels/panel.routes';
import { analyticsReport } from './testing/fixtures/analytics-report';
import { monitoringReport } from './testing/fixtures/monitoring-report';
import { storageReport } from './testing/fixtures/storage-report';
import { configureAdminTestBed } from './testing/admin-test-bed';
import { openAdminPage } from './testing/open-admin-page';
import { sent } from './testing/http/sent';

@Component({ template: '<p class="probe">panel</p>' })
class Probe {}

const OVERVIEW_CALLS = [
  { path: '/api/admin/analytics/report', body: analyticsReport() },
  { path: '/api/admin/monitoring', body: monitoringReport() },
  { path: '/api/admin/storage', body: storageReport() },
] as const;

function setUp(): void {
  configureAdminTestBed([
    { path: 'admin', component: AdminPage, children: [{ path: 'users', component: Probe }] },
  ]);
}

describe('AdminPage', () => {
  it('shows the overview at /admin', async () => {
    setUp();

    const { page } = await openAdminPage('/admin', async (http) => {
      for (const call of OVERVIEW_CALLS) {
        (await sent({ http }, call.path)).flush(call.body);
      }
    });

    expect(page.querySelector('lodb-overview-panel lodb-kpi')).not.toBeNull();
    expect(page.querySelector('.probe')).toBeNull();
  });

  it('shows the panel of the URL instead of the overview', async () => {
    setUp();

    const { page, http } = await openAdminPage('/admin/users');

    expect(page.querySelector('.probe')).not.toBeNull();
    expect(page.querySelector('lodb-overview-panel')).toBeNull();
    http.expectNone(() => true);
  });

  it('shows the outcome of the last action above the panel', async () => {
    setUp();
    const { page, harness } = await openAdminPage('/admin/users');

    TestBed.inject(AdminNotices).post({ tone: 'notice', text: 'Banni.' });
    harness.detectChanges();

    const band = page.querySelector('lodb-notices-outlet lodb-admin-band');
    expect(band?.textContent?.trim()).toBe('Banni.');
  });

  it('lands a bookmark of the legacy journal on the journal, its filters kept', async () => {
    configureAdminTestBed([{ path: 'admin', children: PANEL_ROUTES }]);
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/admin/logs?category=auth&from=2026-09-01');

    expect(router.url).toBe('/admin/journal?category=auth&from=2026-09-01');
  });
});
