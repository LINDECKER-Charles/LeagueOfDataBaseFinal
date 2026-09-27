import { openPanel } from '../../../testing/panels/open-panel';
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

describe('StoragePanel', () => {
  it('weighs the bucket, charts its growth and ranks what fills it', async () => {
    const { page } = await openPanel(StoragePanel, '/admin/storage', [
      { path: STORAGE, body: storageReport() },
    ]);

    expect(texts(page, 'lodb-kpi')).toEqual([
      'admin.storage.kpi.bytes6.00 GB',
      'admin.storage.kpi.objects180 000',
      'admin.storage.kpi.dedup2.00×admin.storage.kpi.saved',
      'admin.storage.kpi.webp90.0 %admin.storage.kpi.webp_size',
    ]);
    expect(page.querySelectorAll('lodb-time-series-chart polyline.ts-line')).toHaveLength(2);
    expect(texts(page, 'lodb-donut-chart lodb-legend li')).toEqual(['data 66.7 %', 'img 33.3 %']);
    expect(texts(page, 'lodb-rank-list li')).toContain('png 1.00 GB');
    expect(texts(page, 'ol li')).toContain('img/splash/Ahri_0.jpg 512.00 KB');
    expect(texts(page, 'tbody td')).toEqual(['16.19.1', '12 000', 'champion, item', '1']);
    expect(page.querySelector('[role="alert"]')).toBeNull();
  });

  it('warns that a report read partly is incomplete', async () => {
    const { page } = await openPanel(StoragePanel, '/admin/storage', [
      { path: STORAGE, body: storageReport({ ok: false, error: 'listing timed out' }) },
    ]);

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('listing timed out');
  });

  it('reads the bucket again, past the cache of the API, on demand', async () => {
    const visit = await openPanel(StoragePanel, '/admin/storage', [
      { path: STORAGE, body: storageReport() },
    ]);

    press(visit, 'admin.actions.refresh');
    const again = await sent(visit, STORAGE);
    expect(again.request.params.get('refresh')).toBe('true');
    await reply(visit, again, storageReport({ objects: 7 }));

    expect(texts(visit.page, 'lodb-kpi')[1]).toBe('admin.storage.kpi.objects7');
  });
});
