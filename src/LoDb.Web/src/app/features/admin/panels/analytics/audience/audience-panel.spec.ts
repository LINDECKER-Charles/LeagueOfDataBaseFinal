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
  it('rings the devices and the sources, then ranks who the visitors are', async () => {
    const { page, calls } = await openPanel(AudiencePanel, '/admin/audience?range=7d', [
      { path: REPORT, body: analyticsReport() },
    ]);

    expect(calls[0]?.request.params.get('range')).toBe('7d');
    const [devices, sources] = [...page.querySelectorAll('lodb-donut-chart')];
    expect(texts(devices!, 'lodb-legend li')).toEqual(['mobile 70.0 %', 'desktop 30.0 %']);
    expect(texts(sources!, 'lodb-legend li')).toEqual(['search 50.0 %', 'direct 50.0 %']);
    expect(texts(page, 'lodb-rank-list li')).toEqual([
      'Firefox 420',
      'Linux 210',
      'France (FR) 900',
      'duckduckgo.com 120',
      'fr 800',
      'fr-FR 700',
    ]);
  });

  it('says a ranking of the period is empty', async () => {
    const { page } = await openPanel(AudiencePanel, '/admin/audience', [
      { path: REPORT, body: analyticsReport({ browser: [], device: [] }) },
    ]);

    expect(texts(page, 'lodb-rank-list')[0]).toBe('admin.common.no_data');
    expect(texts(page, 'lodb-donut-chart')[0]).toBe('admin.chart.empty');
  });
});
