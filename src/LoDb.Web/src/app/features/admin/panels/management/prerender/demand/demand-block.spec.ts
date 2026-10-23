import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { PrerenderDemandReport } from '../../../../../../core/api/generated/models/prerender-demand-report';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { prerenderDemand } from '../../../../testing/fixtures/prerender-demand';
import { sent } from '../../../../testing/http/sent';
import { DemandBlock } from './demand-block';
import { DemandReportStore } from './demand-report-store';

const DEMAND = '/api/admin/prerender/demand';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

async function render(report: PrerenderDemandReport): Promise<HTMLElement> {
  configureAdminTestBed([], [DemandReportStore]);
  const fixture = TestBed.createComponent(DemandBlock);
  fixture.detectChanges();
  const http = TestBed.inject(HttpTestingController);
  (await sent({ http }, DEMAND)).flush(report);
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('DemandBlock', () => {
  it('shows its state, its figures and its most requested pages in their order', async () => {
    const host = await render(prerenderDemand());

    expect(texts(host, '[data-demand-state]')).toEqual(['admin.prerender.demand.states.active']);
    expect(texts(host, '[data-demand-readout] .readout-value')).toEqual([
      '412',
      '64',
      '1 210',
      '2',
    ]);
    expect(
      [...host.querySelectorAll('[data-demand-page]')].map((row) =>
        row.getAttribute('data-demand-page'),
      ),
    ).toEqual(['/fr/15.13.1/champions/Aatrox', '/en/14.1.1/items/1036-long-sword']);
    expect(host.querySelectorAll('[data-demand-attempts]')).toHaveLength(1);
    expect(host.querySelector('[data-demand-why]')).toBeNull();
    expect(host.querySelector('[data-demand-empty]')).toBeNull();
  });

  it('says why a switched-off queue does nothing, and that none waits', async () => {
    const host = await render(
      prerenderDemand({ isEnabled: false, isActive: false, pending: 0, top: [] }),
    );

    expect(texts(host, '[data-demand-state]')).toEqual(['admin.prerender.demand.states.off']);
    expect(texts(host, '[data-demand-why]')).toEqual(['admin.prerender.demand.why.off']);
    expect(texts(host, '[data-demand-empty]')).toEqual(['admin.prerender.demand.top.empty']);
  });
});
