import { analyticsReport } from '../../../testing/fixtures/analytics-report';
import { openPanel } from '../../../testing/panels/open-panel';
import { TrafficPanel } from './traffic-panel';

const REPORT = '/api/admin/analytics/report';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('TrafficPanel', () => {
  it('counts the views of the period, charts them by day and ranks what was viewed', async () => {
    const { page, calls } = await openPanel(TrafficPanel, '/admin/traffic?range=90d', [
      { path: REPORT, body: analyticsReport() },
    ]);

    expect(calls[0]?.request.params.get('range')).toBe('90d');
    expect(texts(page, 'lodb-kpi')).toEqual([
      'admin.traffic.kpi.views 12.3k 30d',
      'admin.traffic.kpi.visitors 2.1k admin.traffic.kpi.visitors_sub',
      'admin.traffic.kpi.pages 88 admin.traffic.kpi.pages_sub',
      'admin.traffic.kpi.per_day 4113 admin.traffic.kpi.per_day_sub',
    ]);
    // The views and the visitors: the robots stay out of the chart, as in the legacy admin.
    expect(page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(2);
    expect(texts(page, 'lodb-rank-list li')).toEqual([
      '/fr/champions 900',
      '/en/items 450',
      'Ahri 300',
      'champion 600',
      'item 400',
      'page 1k',
    ]);
    expect(texts(page, 'lodb-status-table tbody td')).toEqual([
      '200',
      '12 000',
      '0.0 %',
      '404',
      '340',
      '0.0 %',
    ]);
    expect(page.querySelector('a[aria-current="page"]')?.getAttribute('href')).toBe(
      '/admin/traffic?range=90d',
    );
  });

  it('lays the hours of the week out last, the busiest in full cyan', async () => {
    const { page } = await openPanel(TrafficPanel, '/admin/traffic', [
      { path: REPORT, body: analyticsReport() },
    ]);

    const cards = [...page.querySelectorAll('lodb-admin-card')];
    expect(cards.at(-1)?.querySelector('lodb-heatmap')).not.toBeNull();
    const busiest = page.querySelectorAll<HTMLElement>('.heat-cell')[24 + 20];
    expect(busiest?.style.background).toContain('var(--color-hex) 100%');
  });
});
