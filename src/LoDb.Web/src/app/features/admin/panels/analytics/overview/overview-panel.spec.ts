import { HttpStatusCode } from '@angular/common/http';
import { analyticsReport } from '../../../testing/fixtures/analytics-report';
import { monitoringReport } from '../../../testing/fixtures/monitoring-report';
import { storageReport } from '../../../testing/fixtures/storage-report';
import type { AdminVisit } from '../../../testing/admin-visit';
import { openPanel } from '../../../testing/panels/open-panel';
import { OverviewPanel } from './overview-panel';

const REPORT = '/api/admin/analytics/report';
const MONITORING = '/api/admin/monitoring';
const STORAGE = '/api/admin/storage';

function words(page: HTMLElement): string {
  return (page.textContent ?? '').replace(/\s+/g, ' ');
}

function kpis(visit: AdminVisit): string[] {
  return [...visit.page.querySelectorAll('lodb-kpi')].map((kpi) =>
    (kpi.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('OverviewPanel', () => {
  it('shows the traffic, the health of the services and the storage side by side', async () => {
    const visit = await openPanel(OverviewPanel, '/admin', [
      { path: REPORT, body: analyticsReport() },
      { path: MONITORING, body: monitoringReport() },
      { path: STORAGE, body: storageReport() },
    ]);

    expect(kpis(visit)).toEqual(
      expect.arrayContaining([
        'admin.traffic.kpi.views12 340',
        'admin.traffic.kpi.visitors2 100',
        'admin.overview.users5 400admin.overview.users_week',
        'admin.overview.donations315,00 €admin.overview.donations_count',
        'admin.storage.kpi.bytes6.00 GB',
        'admin.storage.kpi.webp90.0 %',
        'admin.storage.kpi.dedup2.00×',
      ]),
    );
    expect(visit.page.querySelectorAll('lodb-sparkline svg')).toHaveLength(2);
    expect(visit.page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(3);
    expect(words(visit.page)).toContain('admin.health.ok');
    expect(words(visit.page)).toContain('admin.health.degraded');
    expect(visit.page.querySelector('a[href="/admin/traffic"]')).not.toBeNull();
  });

  it('asks the report of the period of the URL', async () => {
    const { calls } = await openPanel(OverviewPanel, '/admin?range=7d', [
      { path: REPORT, body: analyticsReport() },
      { path: MONITORING, body: monitoringReport() },
      { path: STORAGE, body: storageReport() },
    ]);

    expect(calls[0]?.request.params.get('range')).toBe('7d');
  });

  it('keeps the other blocks when one report fails', async () => {
    const visit = await openPanel(OverviewPanel, '/admin', [
      { path: REPORT, body: null, status: HttpStatusCode.BadGateway },
      { path: MONITORING, body: monitoringReport({ counters: null }) },
      { path: STORAGE, body: storageReport() },
    ]);

    const alerts = [...visit.page.querySelectorAll('[role="alert"]')];
    expect(alerts.map((alert) => alert.textContent)).toEqual([
      expect.stringContaining('admin.state.server'),
    ]);
    expect(words(visit.page)).toContain('postgres');
    expect(words(visit.page)).not.toContain('admin.overview.users');
    expect(kpis(visit)).toContain('admin.storage.kpi.objects180 000');
  });
});
