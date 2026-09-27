import { openPanel } from '../../../testing/panels/open-panel';
import { openPanelInFrench } from '../../../testing/panels/open-panel-in-french';
import { press } from '../../../testing/dom/press';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { storageReport } from '../../../testing/fixtures/storage-report';
import { StoragePanel } from './storage-panel';

const STORAGE = '/api/admin/storage';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

function row(name: string, bytes: number) {
  return { name, bytes, objects: 2_100, pct: 50 };
}

describe('StoragePanel', () => {
  it('weighs the storage, charts what it takes in and ranks what fills it', async () => {
    const { page } = await openPanel(StoragePanel, '/admin/storage', [
      {
        path: STORAGE,
        body: storageReport({ families: [row('data', 4 * 1024 ** 3), row('blobs', 1024 ** 3)] }),
      },
    ]);

    expect(texts(page, 'lodb-kpi')).toEqual([
      'admin.storage.kpi.bytes 6.00 GB admin.overview.kpi.storage_sub',
      'admin.storage.kpi.blobs 2.1k admin.storage.kpi.blobs_sub',
      'admin.overview.kpi.webp 90 % 8100 / 9000',
      'admin.storage.kpi.dedup 2.00× admin.storage.kpi.saved',
      'admin.overview.kpi.versions 1 admin.storage.kpi.versions_sub',
    ]);
    expect(texts(page, 'lodb-admin-rule')).toEqual([
      'admin.rules.images',
      'admin.rules.data',
      'admin.rules.detail',
    ]);
    // The objects written day by day, one line.
    expect(page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(1);
    expect(page.querySelector('lodb-donut-chart')).toBeNull();
    expect(texts(page, 'lodb-rank-list li')).toEqual(
      expect.arrayContaining(['data 4.00 GB', 'blobs 1.00 GB', 'png 1.00 GB', 'fr_FR 1.00 GB']),
    );
    expect(texts(page, 'lodb-largest-objects tbody tr')).toEqual([
      'img/splash/Ahri_0.jpg 512.00 KB',
    ]);
    expect(texts(page, 'lodb-coverage-matrix li')).toEqual(['16.19.1fr_FR']);
    expect(page.querySelector('[role="alert"]')).toBeNull();
  });

  it('says the storage could not be read in place of the figures', async () => {
    const { page } = await openPanel(StoragePanel, '/admin/storage', [
      { path: STORAGE, body: storageReport({ ok: false, error: 'listing timed out' }) },
    ]);

    expect(page.querySelector('[role="alert"]')).not.toBeNull();
    expect(page.querySelectorAll('lodb-kpi')).toHaveLength(0);
  });

  it('reads the storage again, past the cache of the API, on demand', async () => {
    const visit = await openPanel(StoragePanel, '/admin/storage', [
      { path: STORAGE, body: storageReport() },
    ]);

    press(visit, 'admin.actions.refresh');
    const again = await sent(visit, STORAGE);
    expect(again.request.params.get('refresh')).toBe('true');
    await reply(visit, again, storageReport({ bytes: 1024 }));

    expect(texts(visit.page, 'lodb-kpi')[0]).toContain('1.00 KB');
  });

  it('says why a reading failed in French, the site in English', async () => {
    const visit = await openPanelInFrench(StoragePanel, '/admin/storage', [
      { path: STORAGE, body: storageReport() },
    ]);

    press(visit, 'Rafraîchir');
    await reply(
      visit,
      await sent(visit, STORAGE),
      storageReport({ ok: false, error: 'listing timed out' }),
    );

    expect(visit.page.querySelector('[role="alert"]')?.textContent).toContain(
      'Stockage indisponible : listing timed out',
    );
  });
});
