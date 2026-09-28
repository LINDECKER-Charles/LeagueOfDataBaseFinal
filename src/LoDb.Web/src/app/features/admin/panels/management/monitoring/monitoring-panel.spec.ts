import { monitoringReport } from '../../../testing/fixtures/monitoring-report';
import { openPanel } from '../../../testing/panels/open-panel';
import { openPanelInFrench } from '../../../testing/panels/open-panel-in-french';
import { press } from '../../../testing/dom/press';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { MonitoringPanel } from './monitoring-panel';

const MONITORING = '/api/admin/monitoring';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

describe('MonitoringPanel', () => {
  it('shows a card per probe, the application figures, the volumes, then the process', async () => {
    const { page } = await openPanel(MonitoringPanel, '/admin/monitoring', [
      {
        path: MONITORING,
        body: monitoringReport({
          services: [
            ...monitoringReport().services,
            { name: 'storage-root', status: 'ok', latencyMs: 2, objects: 4_742, bytes: 1024 ** 2 },
          ],
        }),
      },
    ]);

    expect(texts(page, '[data-service="postgres"] [lodbChip]')).toEqual([
      '4 ms',
      '17.2',
      'admin.monitoring.probe.database',
    ]);
    const storage = page.querySelector('[data-service="storage"] span.inline-flex');
    expect(storage?.className).toContain('text-gold');
    expect(texts(page, '[data-service="storage"] p')).toEqual(['slow listing']);
    expect(texts(page, '[data-service="storage-root"] [lodbChip]')).toContain(
      'admin.monitoring.probe.objects',
    );
    expect(texts(page, 'lodb-admin-rule')).toEqual([
      'admin.rules.application',
      'admin.rules.volumes',
      'admin.monitoring.process.title',
      'admin.monitoring.ingestion.title',
    ]);
    expect(texts(page, 'lodb-kpi').slice(0, 6)).toEqual([
      'admin.overview.kpi.users 5 400 admin.overview.kpi.users_sub',
      'admin.overview.kpi.builds 9 800 admin.overview.kpi.builds_sub',
      'admin.monitoring.counters.votes 12 000',
      'admin.overview.kpi.donations 315,00 € admin.overview.kpi.donations_sub',
      'admin.overview.kpi.api_keys 14',
      'admin.monitoring.counters.api 4.2k admin.monitoring.counters.api_sub',
    ]);
    expect(texts(page, 'lodb-kpi')).toEqual(
      expect.arrayContaining([
        'admin.monitoring.process.version 1.4.0 abc1234',
        'admin.monitoring.process.uptime 2 j 3 h',
        'admin.monitoring.process.collections 120 admin.monitoring.process.collections_sub',
      ]),
    );
    expect(texts(page, 'lodb-monitoring-versions tbody tr')[0]).toContain('16.20.1');
    expect(texts(page, 'lodb-admin-card table tbody tr').slice(0, 2)).toEqual([
      'analytics_daily80.00 MB',
      'audit_log20.00 MB',
    ]);
    expect(texts(page, '.readout-value')).toEqual(['3']);
  });

  it('says what the API could not read, in place of its figures', async () => {
    const { page } = await openPanel(MonitoringPanel, '/admin/monitoring', [
      {
        path: MONITORING,
        body: monitoringReport({
          versions: null,
          counters: null,
          tables: [],
          ingestion: {
            versionBacklog: 0,
            onDemandBacklog: 0,
            outboxPending: null,
            outboxDead: null,
          },
        }),
      },
    ]);

    expect(page.querySelector('lodb-monitoring-versions')).toBeNull();
    expect(texts(page, '[role="alert"]')).toEqual(['admin.monitoring.counters.unavailable']);
    expect(texts(page, '.t-empty')).toEqual(['admin.monitoring.tables_empty']);
    expect(texts(page, '.readout-value')).toEqual(['—']);
  });

  it('probes again, past the cache of the API, on demand', async () => {
    const visit = await openPanel(MonitoringPanel, '/admin/monitoring', [
      { path: MONITORING, body: monitoringReport() },
    ]);

    press(visit, 'admin.actions.refresh');
    const again = await sent(visit, MONITORING);
    expect(again.request.params.get('refresh')).toBe('true');
    await reply(visit, again, monitoringReport({ generatedAt: '2026-09-27T11:00:00Z' }));

    expect(visit.page.textContent).toContain('2026-09-27 11:00:00 UTC');
  });

  it('says the counters could not be read in French, the site in English', async () => {
    const visit = await openPanelInFrench(MonitoringPanel, '/admin/monitoring', [
      { path: MONITORING, body: monitoringReport() },
    ]);

    press(visit, 'Rafraîchir');
    await reply(visit, await sent(visit, MONITORING), monitoringReport({ counters: null }));

    expect(visit.page.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'Compteurs indisponibles.',
    );
  });
});
