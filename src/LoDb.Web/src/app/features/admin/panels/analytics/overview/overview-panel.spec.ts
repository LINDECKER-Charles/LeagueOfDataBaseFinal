import { HttpStatusCode } from '@angular/common/http';
import { ADMIN_API } from '../../../testing/admin-api';
import type { AdminVisit } from '../../../testing/admin-visit';
import { notice } from '../../../testing/dom/notice';
import { press } from '../../../testing/dom/press';
import { analyticsReport } from '../../../testing/fixtures/analytics-report';
import { monitoringReport } from '../../../testing/fixtures/monitoring-report';
import { storageReport } from '../../../testing/fixtures/storage-report';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { settle } from '../../../testing/http/settle';
import { openPanel } from '../../../testing/panels/open-panel';
import { OverviewPanel } from './overview-panel';

const REPORT = '/api/admin/analytics/report';
const MONITORING = '/api/admin/monitoring';
const STORAGE = '/api/admin/storage';
const ROLLUP = '/api/admin/analytics/rollup';

function words(node: Node | null | undefined): string {
  return (node?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function kpis(visit: AdminVisit): string[] {
  return [...visit.page.querySelectorAll('lodb-kpi, a[lodbKpi]')].map(words);
}

function open(url = '/admin') {
  return openPanel(OverviewPanel, url, [
    { path: MONITORING, body: monitoringReport() },
    { path: REPORT, body: analyticsReport() },
    { path: STORAGE, body: storageReport() },
  ]);
}

describe('OverviewPanel', () => {
  it('shows the application, its traffic, then its storage, as the legacy console', async () => {
    const visit = await open();

    expect(kpis(visit)).toEqual([
      'admin.overview.kpi.users 5 400 admin.overview.kpi.users_sub',
      'admin.overview.kpi.builds 9 800 admin.overview.kpi.builds_sub',
      'admin.overview.kpi.donations 315,00 € admin.overview.kpi.donations_sub',
      'admin.overview.kpi.api_keys 14 admin.overview.kpi.api_keys_sub',
      // The badges sit apart by the gap of their row, not by a space.
      'admin.overview.kpi.services postgresstorage admin.overview.kpi.services_sub',
      'admin.overview.kpi.views 12.3k admin.overview.kpi.views_sub',
      'admin.overview.kpi.visitors 2.1k admin.overview.kpi.visitors_sub',
      'admin.overview.kpi.top_page 900 /fr/champions',
      'admin.overview.kpi.storage 6.00 GB admin.overview.kpi.storage_sub',
      'admin.overview.kpi.versions 1 admin.overview.kpi.versions_sub',
      'admin.overview.kpi.webp 90 % admin.overview.kpi.webp_sub',
    ]);
    expect([...visit.page.querySelectorAll('lodb-admin-rule')].map((rule) => words(rule))).toEqual([
      'admin.rules.traffic',
      'admin.rules.storage',
    ]);
    expect(visit.page.querySelectorAll('lodb-sparkline svg')).toHaveLength(2);
    expect(visit.page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(2);
    expect(visit.page.querySelector('a[lodbKpi]')?.getAttribute('href')).toBe('/admin/monitoring');
    expect(visit.page.querySelector('a[href="/admin/traffic?range=30d"]')).not.toBeNull();
    expect(visit.page.querySelector('a[href="/admin/audience?range=30d"]')).not.toBeNull();
  });

  it('tints each probe of the services tile by its health', async () => {
    const visit = await open();

    const badges = [...visit.page.querySelectorAll('a[lodbKpi] span.inline-flex')];
    expect(badges.map((badge) => badge.className.includes('text-good'))).toEqual([true, false]);
    expect(badges[1]?.className).toContain('text-gold');
  });

  it('asks the report of the period of the URL', async () => {
    const { calls } = await open('/admin?range=7d');

    expect(calls[1]?.request.params.get('range')).toBe('7d');
  });

  it('keeps the other blocks when one report fails, and says what it could not read', async () => {
    const visit = await openPanel(OverviewPanel, '/admin', [
      { path: MONITORING, body: monitoringReport({ counters: null }) },
      { path: REPORT, body: null, status: HttpStatusCode.BadGateway },
      { path: STORAGE, body: storageReport({ ok: false, error: 'root unreadable' }) },
    ]);

    const alerts = [...visit.page.querySelectorAll('[role="alert"]')].map(words);
    expect(alerts).toEqual([
      'admin.overview.counters_unavailable',
      'admin.state.unavailable admin.state.retry',
      'admin.storage.unavailable',
    ]);
    expect(kpis(visit)).toEqual([]);
  });

  it('consolidates the last days, says how many, and reads the traffic again', async () => {
    const visit = await open();

    press(visit, 'admin.overview.roll_up');
    (await sent(visit, ROLLUP, 'POST')).flush({ days: ['2026-09-02', '2026-09-03'] });
    await reply(
      visit,
      await sent(visit, REPORT),
      analyticsReport({ totals: { ...analyticsReport().totals, views: 1 } }),
    );

    expect(notice()).toBe('notice: admin.overview.rolled_up');
    expect(kpis(visit)[5]).toBe('admin.overview.kpi.views 1 admin.overview.kpi.views_sub');
  });

  it('tells a refused consolidation and keeps the report as it was', async () => {
    const visit = await open();

    press(visit, 'admin.overview.roll_up');
    (await sent(visit, ROLLUP, 'POST')).flush(
      { code: 'forbidden' },
      { status: HttpStatusCode.Forbidden, statusText: 'Forbidden' },
    );
    await vi.waitFor(() => expect(notice()).toBe('alert: admin.errors.session'));
    await settle(visit);

    visit.http.expectNone(`${ADMIN_API}${REPORT}?range=30d`);
    expect(visit.page.querySelector<HTMLButtonElement>('lodb-page-head button')?.disabled).toBe(
      false,
    );
  });
});
