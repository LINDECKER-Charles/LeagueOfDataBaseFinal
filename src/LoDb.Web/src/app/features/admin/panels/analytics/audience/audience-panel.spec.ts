import { analyticsReport } from '../../../testing/fixtures/analytics-report';
import { openPanel } from '../../../testing/panels/open-panel';
import { AudiencePanel } from './audience-panel';

const REPORT = '/api/admin/analytics/report';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('AudiencePanel', () => {
  it('counts the visitors, rings the devices and the sources, then ranks the rest', async () => {
    const { page, calls } = await openPanel(AudiencePanel, '/admin/audience?range=7d', [
      { path: REPORT, body: analyticsReport() },
    ]);

    expect(calls[0]?.request.params.get('range')).toBe('7d');
    expect(texts(page, 'lodb-kpi')).toEqual([
      'admin.audience.kpi.visitors 2.1k 30d',
      'admin.audience.kpi.returning 640 admin.audience.kpi.returning_sub',
      'admin.audience.kpi.new 1.5k admin.audience.kpi.new_sub',
      'admin.audience.kpi.bots 310 admin.audience.kpi.bots_sub',
    ]);
    const [devices, sources] = [...page.querySelectorAll('lodb-donut-chart')];
    expect(texts(devices!, 'lodb-legend li')).toEqual(['mobile 700', 'desktop 300']);
    expect(
      [...devices!.querySelectorAll('circle[stroke-dasharray]')].map((arc) =>
        arc.getAttribute('stroke'),
      ),
    ).toEqual(['var(--color-series-blue)', 'var(--color-gold)']);
    expect(texts(sources!, 'lodb-legend li')).toEqual(['search 500', 'direct 500']);
    expect(texts(page, 'lodb-rank-list li')).toEqual([
      'France 900',
      'Firefox 420',
      'Linux 210',
      'fr-FR 700',
      'duckduckgo.com 120',
    ]);
  });

  it('says why no country shows when the instance has no GeoLite2 database', async () => {
    const { page } = await openPanel(AudiencePanel, '/admin/audience', [
      { path: REPORT, body: analyticsReport({ geoAvailable: false, country: [] }) },
    ]);

    expect(texts(page, '[lodbBadge]')).toEqual(['admin.audience.geo_badge']);
    expect(page.textContent).toContain('LoDb:Analytics:GeoIpDatabase');
  });

  it('says a ranking of the period is empty', async () => {
    const { page } = await openPanel(AudiencePanel, '/admin/audience', [
      { path: REPORT, body: analyticsReport({ browser: [], device: [] }) },
    ]);

    expect(texts(page, 'lodb-rank-list')[1]).toBe('admin.common.no_data');
    expect(texts(page, 'lodb-donut-chart')[0]).toBe('admin.chart.empty');
  });
});
