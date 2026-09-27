import { HttpStatusCode } from '@angular/common/http';
import { analyticsReport } from '../../../testing/fixtures/analytics-report';
import { ADMIN_API } from '../../../testing/admin-api';
import { openPanel } from '../../../testing/panels/open-panel';
import { press } from '../../../testing/dom/press';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { settle } from '../../../testing/http/settle';
import { toasts } from '../../../testing/dom/toasts';
import { TrafficPanel } from './traffic-panel';

const REPORT = '/api/admin/analytics/report';
const ROLLUP = '/api/admin/analytics/rollup';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('TrafficPanel', () => {
  it('charts the views of the period day by day and hour by hour, then ranks them', async () => {
    const { page, calls } = await openPanel(TrafficPanel, '/admin/traffic?range=90d', [
      { path: REPORT, body: analyticsReport() },
    ]);

    expect(calls[0]?.request.params.get('range')).toBe('90d');
    expect(texts(page, 'lodb-kpi')[0]).toBe('admin.traffic.kpi.views12 340admin.traffic.kpi.pages');
    expect(page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(3);
    const busiest = page.querySelectorAll<HTMLElement>('.heat-cell')[24 + 20];
    expect(busiest?.style.background).toContain('var(--color-hex) 100%');
    expect(texts(page, 'lodb-rank-list li')).toEqual(
      expect.arrayContaining(['/fr/champions 900', '/en/items 450', '404 340']),
    );
    expect(texts(page, 'lodb-donut-chart lodb-legend li')).toEqual([
      'champion 60.0 %',
      'item 40.0 %',
    ]);
    expect(page.querySelector('a[aria-current="page"]')?.getAttribute('href')).toBe(
      '/admin/traffic?range=90d',
    );
  });

  it('consolidates the last days, says how many, and reads the report again', async () => {
    const visit = await openPanel(TrafficPanel, '/admin/traffic', [
      { path: REPORT, body: analyticsReport() },
    ]);

    press(visit, 'admin.traffic.roll_up');
    const rollup = await sent(visit, ROLLUP, 'POST');
    rollup.flush({ days: ['2026-09-02', '2026-09-03'] });
    const again = await sent(visit, REPORT);
    await reply(
      visit,
      again,
      analyticsReport({ totals: { ...analyticsReport().totals, views: 1 } }),
    );

    expect(toasts()).toEqual(['success: admin.traffic.rolled_up']);
    expect(texts(visit.page, 'lodb-kpi')[0]).toBe(
      'admin.traffic.kpi.views1admin.traffic.kpi.pages',
    );
  });

  it('tells a refused consolidation and keeps the report as it was', async () => {
    const visit = await openPanel(TrafficPanel, '/admin/traffic', [
      { path: REPORT, body: analyticsReport() },
    ]);

    press(visit, 'admin.traffic.roll_up');
    (await sent(visit, ROLLUP, 'POST')).flush(
      { code: 'forbidden' },
      { status: HttpStatusCode.Forbidden, statusText: 'Forbidden' },
    );
    await vi.waitFor(() => expect(toasts()).toEqual(['error: admin.errors.session']));
    await settle(visit);

    visit.http.expectNone(`${ADMIN_API}${REPORT}?range=30d`);
    expect(visit.page.querySelector<HTMLButtonElement>('lodb-page-head button')?.disabled).toBe(
      false,
    );
  });
});
