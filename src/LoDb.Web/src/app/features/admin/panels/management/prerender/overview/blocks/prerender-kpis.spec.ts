import { TestBed } from '@angular/core/testing';
import type { PrerenderReport } from '../../../../../../../core/api/generated/models/prerender-report';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import { prerenderReport } from '../../../../../testing/fixtures/prerender-report';
import { PrerenderKpis } from './prerender-kpis';

const BASE = prerenderReport();

async function storedSub(report: PrerenderReport): Promise<string> {
  configureAdminTestBed();
  const fixture = TestBed.createComponent(PrerenderKpis);
  fixture.componentRef.setInput('report', report);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  const host = fixture.nativeElement as HTMLElement;
  return (host.querySelector('[data-kpi="stored"]')?.textContent ?? '').replace(/\s+/g, ' ');
}

// The fixture's epoch 2 is served, epoch 1 retired: a retired epoch's queue pages are left
// out with the epoch.
function withQueue(served: number, retired: number): PrerenderReport {
  const epochs = BASE.storage.epochs.map((epoch) => ({
    ...epoch,
    queuePages: epoch.state === 'retired' ? retired : served,
  }));
  return { ...BASE, storage: { ...BASE.storage, epochs } };
}

describe('PrerenderKpis', () => {
  it('names the share of the stored pages the automatic queue wrote', async () => {
    const sub = await storedSub(withQueue(1_790, 300));

    expect(sub).toContain('kpis.stored_sub_queue');
  });

  it('says nothing of the queue while only a retired epoch holds its pages', async () => {
    const sub = await storedSub(withQueue(0, 300));

    expect(sub).toContain('kpis.stored_sub ');
  });
});
