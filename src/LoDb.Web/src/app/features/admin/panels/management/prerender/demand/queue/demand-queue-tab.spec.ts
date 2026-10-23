import { HttpTestingController } from '@angular/common/http/testing';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderDemandPages } from '../../../../../../../core/api/generated/models/prerender-demand-pages';
import { configureAdminTestBed } from '../../../../../testing/admin-test-bed';
import {
  prerenderDemand,
  prerenderDemandPages,
} from '../../../../../testing/fixtures/prerender-demand';
import { sent } from '../../../../../testing/http/sent';
import { DemandReportStore } from '../demand-report-store';
import { DemandQueueTab } from './demand-queue-tab';

const DEMAND = '/api/admin/prerender/demand';
const PAGES = `${DEMAND}/pages`;

interface Opened {
  readonly fixture: ComponentFixture<DemandQueueTab>;
  readonly host: HTMLElement;
  readonly http: HttpTestingController;
}

async function render(pages: PrerenderDemandPages = prerenderDemandPages()): Promise<Opened> {
  configureAdminTestBed([], [DemandReportStore]);
  const fixture = TestBed.createComponent(DemandQueueTab);
  fixture.detectChanges();
  const http = TestBed.inject(HttpTestingController);
  (await sent({ http }, DEMAND)).flush(prerenderDemand());
  (await sent({ http }, PAGES)).flush(pages);
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, host: fixture.nativeElement as HTMLElement, http };
}

async function answer(opened: Opened, pages: PrerenderDemandPages): Promise<URLSearchParams> {
  const request = await sent({ http: opened.http }, PAGES);
  request.flush(pages);
  await opened.fixture.whenStable();
  opened.fixture.detectChanges();
  return new URLSearchParams(request.request.params.toString());
}

function pressed(host: HTMLElement): (string | null)[] {
  return [...host.querySelectorAll('[data-queue-state][aria-pressed="true"]')].map((chip) =>
    chip.getAttribute('data-queue-state'),
  );
}

describe('DemandQueueTab', () => {
  it('lists the written pages first, with what each holds and a link to the site', async () => {
    const { host } = await render();

    expect(pressed(host)).toEqual(['done']);
    expect(host.querySelectorAll('[data-queue-state]')).toHaveLength(5);
    const rows = [...host.querySelectorAll('[data-queue-page]')];
    expect(rows.map((row) => row.getAttribute('data-queue-page'))).toEqual([
      '/fr/15.13.1/champions/Aatrox',
      '/en/14.1.1/items/1036-long-sword',
    ]);
    const link = rows[0].querySelector('a');
    expect([link?.getAttribute('href'), link?.getAttribute('target')]).toEqual([
      '/fr/15.13.1/champions/Aatrox',
      '_blank',
    ]);
    expect(rows[0].querySelector('[data-queue-class]')?.textContent).toContain('revisable');
    expect(rows[0].querySelector('[data-queue-superseded]')).toBeNull();
    expect(rows[1].querySelector('[data-queue-superseded]')).not.toBeNull();
    expect(host.querySelectorAll('[data-hours-table] tbody tr')).toHaveLength(24);
    expect(host.querySelector('[data-queue-epoch]')?.textContent).toContain('queue.epoch');
  });

  it('reads another state from its first page, and says when none is in it', async () => {
    const opened = await render(prerenderDemandPages({ total: 120 }));
    (opened.host.querySelector('[data-queue-next]') as HTMLButtonElement).click();
    await answer(opened, prerenderDemandPages({ page: 2, total: 120 }));

    (opened.host.querySelector('[data-queue-state="failed"]') as HTMLButtonElement).click();
    opened.fixture.detectChanges();
    const query = await answer(
      opened,
      prerenderDemandPages({ state: 'failed', total: 0, pages: [] }),
    );

    expect([query.get('state'), query.get('page')]).toEqual(['failed', '1']);
    expect(pressed(opened.host)).toEqual(['failed']);
    expect(opened.host.querySelector('[data-queue-empty]')).not.toBeNull();
    expect(opened.host.querySelector('[data-queue-pager]')).toBeNull();
  });

  it('turns the pages of a long list', async () => {
    const opened = await render(prerenderDemandPages({ total: 120 }));

    expect(opened.host.querySelector('[data-queue-pager]')?.textContent).toContain('1 / 3');
    (opened.host.querySelector('[data-queue-next]') as HTMLButtonElement).click();
    const query = await answer(opened, prerenderDemandPages({ page: 2, total: 120 }));

    expect(query.get('page')).toBe('2');
    expect(opened.host.querySelector('[data-queue-pager]')?.textContent).toContain('2 / 3');
    expect((opened.host.querySelector('[data-queue-previous]') as HTMLButtonElement).disabled).toBe(
      false,
    );
  });
});
