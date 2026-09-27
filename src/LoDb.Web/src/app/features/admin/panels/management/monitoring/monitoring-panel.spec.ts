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
  it('shows the probes, the process, the queues, the versions and the tables', async () => {
    const { page } = await openPanel(MonitoringPanel, '/admin/monitoring', [
      { path: MONITORING, body: monitoringReport() },
    ]);

    expect(texts(page, '[data-service="postgres"] td')).toEqual([
      'postgres',
      'admin.health.ok',
      '4 ms',
      '17.2',
      '3.00 GB',
      '—',
    ]);
    const storage = page.querySelector('[data-service="storage"] td:nth-child(2) span');
    expect(storage?.className).toContain('text-gold-light');
    expect(texts(page, 'lodb-kpi')).toEqual(
      expect.arrayContaining([
        'admin.monitoring.process.version1.4.0abc1234',
        'admin.monitoring.process.uptime2 j 3 h',
        'admin.monitoring.process.collections120 / 14 / 2',
        'admin.monitoring.ingestion.outbox_dead1',
      ]),
    );
    expect(texts(page, 'lodb-monitoring-versions tbody tr')[0]).toContain('16.20.1');
    expect(texts(page, 'lodb-rank-list li')).toEqual([
      'analytics_daily 80.00 MB',
      'audit_log 20.00 MB',
    ]);
  });

  it('leaves out the sections the API could not read', async () => {
    const { page } = await openPanel(MonitoringPanel, '/admin/monitoring', [
      {
        path: MONITORING,
        body: monitoringReport({
          versions: null,
          counters: null,
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
    expect(page.textContent).not.toContain('admin.monitoring.counters.title');
    expect(texts(page, 'lodb-kpi')).toContain('admin.monitoring.ingestion.outbox_pending—');
  });

  it('probes again, past the cache of the API, on demand', async () => {
    const visit = await openPanel(MonitoringPanel, '/admin/monitoring', [
      { path: MONITORING, body: monitoringReport() },
    ]);

    press(visit, 'admin.actions.refresh');
    const again = await sent(visit, MONITORING);
    expect(again.request.params.get('refresh')).toBe('true');
    await reply(visit, again, monitoringReport({ generatedAt: '2026-09-27T11:00:00Z' }));

    expect(visit.page.textContent).toContain('admin.common.generated_at');
  });

  it('dates a new reading in French, the site in English', async () => {
    const visit = await openPanelInFrench(MonitoringPanel, '/admin/monitoring', [
      { path: MONITORING, body: monitoringReport() },
    ]);
    expect(visit.page.textContent).toContain('Relevé du 27/09/2026 10:15:30 (UTC)');

    press(visit, 'Actualiser');
    await reply(
      visit,
      await sent(visit, MONITORING),
      monitoringReport({ generatedAt: '2026-09-27T11:00:00Z' }),
    );

    expect(visit.page.textContent).toContain('Relevé du 27/09/2026 11:00:00 (UTC)');
    expect(visit.page.textContent).not.toContain('Read at');
  });
});
