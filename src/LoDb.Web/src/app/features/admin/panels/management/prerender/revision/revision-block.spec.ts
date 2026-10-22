import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import type { PrerenderRevisionReport } from '../../../../../../core/api/generated/models/prerender-revision-report';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { prerenderRevision } from '../../../../testing/fixtures/revision/prerender-revision';
import { sent } from '../../../../testing/http/sent';
import { RevisionBlock } from './revision-block';

const REVISION = '/api/admin/prerender/revision';

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

async function render(report: PrerenderRevisionReport): Promise<HTMLElement> {
  configureAdminTestBed();
  const fixture = TestBed.createComponent(RevisionBlock);
  fixture.detectChanges();
  const http = TestBed.inject(HttpTestingController);
  (await sent({ http }, REVISION)).flush(report);
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('RevisionBlock', () => {
  it('shows its state, its figures and its epochs, the newest first', async () => {
    const host = await render(prerenderRevision());

    expect(texts(host, '[data-revision-state]')).toEqual([
      'admin.prerender.revision.states.active',
    ]);
    expect(texts(host, '[data-revision-readout] .readout-value')).toEqual([
      '1 806',
      '840',
      '22 430',
      '3',
    ]);
    expect(
      [...host.querySelectorAll('[data-revision-epoch]')].map((row) =>
        row.getAttribute('data-revision-epoch'),
      ),
    ).toEqual(['3', '2']);
    expect(host.querySelector('[data-revision-why]')).toBeNull();
  });

  it('says why it renders nothing without a store, and that it holds no page', async () => {
    const host = await render(
      prerenderRevision({ isActive: false, pending: 0, epochs: [], nextDueAt: null }),
    );

    expect(texts(host, '[data-revision-state]')).toEqual([
      'admin.prerender.revision.states.dormant',
    ]);
    expect(texts(host, '[data-revision-why]')).toEqual(['admin.prerender.revision.why.dormant']);
    expect(texts(host, '[data-revision-empty]')).toEqual(['admin.prerender.revision.empty']);
  });
});
