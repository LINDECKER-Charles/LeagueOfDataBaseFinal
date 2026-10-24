import { analyticsReport } from '../../../testing/fixtures/analytics-report';
import { robotReport } from '../../../testing/fixtures/robot-report';
import { openPanel } from '../../../testing/panels/open-panel';
import type { PanelCall } from '../../../testing/panels/panel-call';
import { TrafficPanel } from './traffic-panel';

const REPORT = '/api/admin/analytics/report';
const ROBOTS = '/api/admin/analytics/robots';
const BREAKDOWN = '/api/admin/analytics/breakdown';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

// The report, the robots of the same period, then the explorer once the page is laid out.
function calls(report = analyticsReport()): PanelCall[] {
  return [
    { path: REPORT, body: report },
    { path: ROBOTS, body: robotReport({ dates: ['2026-09-01', '2026-09-02', '2026-09-03'] }) },
    {
      path: BREAKDOWN,
      body: {
        range: '30d',
        dimension: 'pages',
        total: 1_350,
        matched: 2,
        rows: [
          { name: '/fr/champions', count: 900, pct: 66.7 },
          { name: '/en/items', count: 450, pct: 33.3 },
        ],
        page: 1,
        hasMore: false,
      },
    },
  ];
}

describe('TrafficPanel', () => {
  it('counts the views of the period, charts them with the robots, ranks what was viewed', async () => {
    const { page, calls: sent } = await openPanel(
      TrafficPanel,
      '/admin/traffic?range=90d',
      calls(),
    );

    expect(sent.map((call) => call.request.params.get('range'))).toEqual(['90d', '90d', '90d']);
    expect(texts(page, 'lodb-kpi')).toEqual([
      'admin.traffic.kpi.views 12.3k admin.traffic.trend.none',
      'admin.traffic.kpi.visitors 2.1k admin.traffic.trend.none',
      'admin.traffic.kpi.per_day 4 113 admin.traffic.kpi.visitors_sub',
      'admin.traffic.kpi.pages 88 admin.traffic.kpi.pages_sub',
    ]);
    const robots = page.querySelector('a[lodbKpi]');
    expect(robots?.getAttribute('href')).toBe('/admin/robots?range=90d');
    expect(robots?.textContent).toContain('4k');
    // The views, the visitors, and the robots' hits of the same dates.
    expect(page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(3);
    expect(texts(page, 'lodb-rank-list li')).toEqual([
      'Ahri 300',
      'champion 600',
      'item 400',
      'page 1k',
      'fr-FR 700',
      'fr 800',
    ]);
    expect(texts(page, 'lodb-status-table tbody td')).toEqual([
      '200',
      '12 000',
      '0.0 %',
      '404',
      '340',
      '0.0 %',
    ]);
    expect(texts(page, 'lodb-traffic-explorer tbody td.num').slice(0, 2)).toEqual([
      '900',
      '66.7 %',
    ]);
    expect(page.querySelector('a[aria-current="page"]')?.getAttribute('href')).toBe(
      '/admin/traffic?range=90d',
    );
  });

  it('says how the views moved since the period before', async () => {
    const report = analyticsReport({
      previous: {
        views: 10_000,
        uniqueVisitors: 2_100,
        returningVisitors: 0,
        botViews: 0,
        pagesTracked: 0,
      },
    });

    const { page } = await openPanel(TrafficPanel, '/admin/traffic', calls(report));

    expect(texts(page, 'lodb-kpi').slice(0, 2)).toEqual([
      'admin.traffic.kpi.views 12.3k admin.traffic.trend.up',
      'admin.traffic.kpi.visitors 2.1k admin.traffic.trend.flat',
    ]);
  });

  it('lays the hours of the week out last, the busiest in full cyan', async () => {
    const { page } = await openPanel(TrafficPanel, '/admin/traffic', calls());

    const cards = [...page.querySelectorAll('lodb-admin-card')];
    expect(cards.at(-1)?.querySelector('lodb-heatmap')).not.toBeNull();
    const busiest = page.querySelectorAll<HTMLElement>('.heat-cell')[24 + 20];
    expect(busiest?.style.background).toContain('var(--color-hex) 100%');
  });
});
