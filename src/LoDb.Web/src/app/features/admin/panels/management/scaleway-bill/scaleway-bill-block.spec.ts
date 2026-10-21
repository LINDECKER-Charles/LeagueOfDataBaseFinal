import { HttpStatusCode } from '@angular/common/http';
import { HttpTestingController } from '@angular/common/http/testing';
import type { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ScalewayBillReport } from '../../../../../core/api/generated/models/scaleway-bill-report';
import type { ScalewayProjectBill } from '../../../../../core/api/generated/models/scaleway-project-bill';
import { configureAdminTestBed } from '../../../testing/admin-test-bed';
import { press } from '../../../testing/dom/press';
import { backupReport } from '../../../testing/fixtures/backup-report';
import { prerenderDemand } from '../../../testing/fixtures/prerender-demand';
import { prerenderPatches } from '../../../testing/fixtures/prerender-patches';
import { prerenderReport } from '../../../testing/fixtures/prerender-report';
import { scalewayBillReport } from '../../../testing/fixtures/scaleway-bill-report';
import { reply } from '../../../testing/http/reply';
import { sent } from '../../../testing/http/sent';
import { openPanel } from '../../../testing/panels/open-panel';
import { openPanelInFrench } from '../../../testing/panels/open-panel-in-french';
import type { PanelCall } from '../../../testing/panels/panel-call';
import { BackupsPanel } from '../backups/backups-panel';
import { PrerenderPanel } from '../prerender/prerender-panel';
import type { BillProject } from './bill-view';
import { ScalewayBillBlock } from './scaleway-bill-block';

const BILL = '/api/admin/scaleway/bill';

interface BilledPanel {
  readonly project: BillProject;
  readonly panel: Type<unknown>;
  readonly url: string;
  /** The report of the panel, which comes first. */
  readonly report: PanelCall;
  /** What the panel reads after its report and before the bill. */
  readonly before: readonly PanelCall[];
}

const BACKUPS: BilledPanel = {
  project: 'backups',
  panel: BackupsPanel,
  url: '/admin/backups',
  report: { path: '/api/admin/backups', body: backupReport() },
  before: [],
};
const PAGES: BilledPanel = {
  project: 'pages',
  panel: PrerenderPanel,
  url: '/admin/prerender',
  // No pass runs: the panel does not read itself again meanwhile.
  report: { path: '/api/admin/prerender', body: prerenderReport({ current: null }) },
  // The coverage of the served epoch (the « Couverture » tab renders at once), and the
  // automatic queue under the key figures.
  before: [
    { path: '/api/admin/prerender/patches', body: prerenderPatches() },
    { path: '/api/admin/prerender/demand', body: prerenderDemand() },
  ],
};

function texts(root: ParentNode, selector: string): string[] {
  return [...root.querySelectorAll(selector)].map((node) =>
    (node.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

/** `bill` with nothing consumed over its six months. */
function unbilled(bill: ScalewayProjectBill | null | undefined): ScalewayProjectBill {
  const periods = (bill?.periods ?? []).map((month) => ({ ...month, total: 0, lines: [] }));
  return { projectId: bill?.projectId ?? '', periods };
}

describe.each([BACKUPS, PAGES])(
  'the Scaleway bill of the $project',
  ({ project, panel, url, report, before }) => {
    const block = `lodb-scaleway-bill[data-bill="${project}"]`;

    function open(bill: ScalewayBillReport | null, status?: number) {
      return openPanel(panel, url, [report, ...before, { path: BILL, body: bill, status }]);
    }

    it('says in one line which setting turns it on while the key is not set', async () => {
      const { page, calls } = await open({ configured: false, stale: false });

      expect(calls[1 + before.length]?.request.method).toBe('GET');
      expect(texts(page, `${block} [data-bill-state]`)).toEqual([
        'admin.scaleway_bill.not_configured',
      ]);
      expect(page.querySelector(`${block} lodb-admin-card`)).toBeNull();
    });

    it('holds a single line while the bill loads, no card', async () => {
      // On its own: a panel is stable, hence rendered, only once the bill has answered.
      configureAdminTestBed();
      const fixture = TestBed.createComponent(ScalewayBillBlock);
      fixture.componentRef.setInput('project', project);
      fixture.detectChanges();
      const host = fixture.nativeElement as HTMLElement;

      expect(host.querySelectorAll('[data-bill-state="loading"]')).toHaveLength(1);
      expect(host.querySelector('lodb-admin-card')).toBeNull();
      const http = TestBed.inject(HttpTestingController);
      (await sent({ http }, BILL)).flush({ configured: false, stale: false });
      await fixture.whenStable();
      fixture.detectChanges();
      expect(texts(host, '[data-bill-state]')).toEqual(['admin.scaleway_bill.not_configured']);
    });

    it('says Scaleway has not answered yet, and reads again on demand', async () => {
      const unavailable = { configured: true, stale: true, backups: null, pages: null };
      const visit = await open(unavailable);

      expect(texts(visit.page, `${block} [data-bill-state] span`)).toEqual([
        'admin.scaleway_bill.unavailable',
      ]);
      expect(visit.page.querySelector(`${block} [data-bill-stale]`)).toBeNull();
      expect(visit.page.querySelector(`${block} [data-bill-fetched]`)).toBeNull();
      expect(texts(visit.page, `${block} [data-bill-note]`)).toEqual([
        `admin.scaleway_bill.notes.${project}`,
      ]);
      press(visit, 'admin.scaleway_bill.read_again');
      await reply(visit, await sent(visit, BILL), scalewayBillReport());
      expect(visit.page.querySelector(`${block} [data-bill-state="billed"]`)).not.toBeNull();
    });

    it('keeps the amounts of the last good reading, dated, and reads again on demand', async () => {
      const visit = await open(scalewayBillReport({ stale: true }));

      expect(texts(visit.page, `${block} [data-bill-stale] span`)).toEqual([
        'admin.scaleway_bill.stale',
      ]);
      expect(visit.page.querySelector(`${block} [data-bill-state="billed"]`)).not.toBeNull();
      // The banner dates the reading: the line under the bill does not say it again.
      expect(visit.page.querySelector(`${block} [data-bill-fetched]`)).toBeNull();
      press(visit, 'admin.scaleway_bill.read_again');
      await reply(visit, await sent(visit, BILL), scalewayBillReport());
      expect(visit.page.querySelector(`${block} [data-bill-stale]`)).toBeNull();
      expect(texts(visit.page, `${block} [data-bill-fetched]`)).toEqual([
        'admin.scaleway_bill.fetched',
      ]);
    });

    it('says nothing was consumed yet over the six months', async () => {
      const bill = scalewayBillReport();
      const { page } = await open({ ...bill, [project]: unbilled(bill[project]) });

      expect(texts(page, `${block} [data-bill-state]`)).toEqual(['admin.scaleway_bill.empty']);
      expect(page.querySelector(`${block} tr[data-bill-period]`)).toBeNull();
    });

    it('fails on its own, the panel shown all the same, then tries again', async () => {
      const visit = await open(null, HttpStatusCode.BadGateway);

      expect(visit.page.querySelectorAll('lodb-kpi')).toHaveLength(4);
      expect(texts(visit.page, `${block} [data-bill-state] span`)).toEqual([
        'admin.scaleway_bill.failed',
      ]);
      press(visit, 'admin.state.retry');
      await reply(visit, await sent(visit, BILL), scalewayBillReport());
      expect(visit.page.querySelector(`${block} [data-bill-state="billed"]`)).not.toBeNull();
    });
  },
);

describe('the Scaleway bill of a panel', () => {
  it('shows the backups month by month since the first one billed, and its lines', async () => {
    const { page } = await openPanel(BACKUPS.panel, BACKUPS.url, [
      BACKUPS.report,
      { path: BILL, body: scalewayBillReport() },
    ]);

    expect(texts(page, '[data-bill-readout] .readout-value')).toEqual(['0,00 €', '0,0517 €']);
    expect(texts(page, '[data-bill-readout="previous"] .readout-text')).toEqual(['septembre 2026']);
    // July to May 2026 come before the first month billed: they do not show.
    expect(texts(page, 'tr[data-bill-period] td')).toEqual([
      'octobre 2026',
      '0,00 €',
      'septembre 2026',
      '0,0517 €',
    ]);
    expect(texts(page, 'tr[data-bill-line]')).toHaveLength(5);
    expect(texts(page, 'tr[data-bill-line] td')).toEqual(
      expect.arrayContaining(['196 gigabyte_hour', '0,01 €']),
    );
    expect(texts(page, 'tr[data-bill-line][data-deduction] td:last-child')).toEqual([
      '-0,01 €',
      '-0,01 €',
    ]);
    expect(texts(page, '[data-bill-note]')).toEqual(['admin.scaleway_bill.notes.backups']);
    expect(texts(page, '[data-bill-fetched]')).toEqual(['admin.scaleway_bill.fetched']);
  });

  it('shows the pages in their store section, with no previous month billed yet', async () => {
    const { page } = await openPanel(PAGES.panel, PAGES.url, [
      PAGES.report,
      ...PAGES.before,
      { path: BILL, body: scalewayBillReport() },
    ]);

    expect(page.querySelector('lodb-costs-tab lodb-scaleway-bill')).not.toBeNull();
    expect(texts(page, '[data-bill-readout="previous"] > *')).toEqual([
      'admin.scaleway_bill.previous',
      '—',
      'admin.scaleway_bill.previous_none',
    ]);
    expect(texts(page, 'tr[data-bill-period] td')).toEqual(['octobre 2026', '0,00 €']);
    // The tab adds its own note above the block: only the block's is read here.
    expect(texts(page, 'lodb-scaleway-bill [data-bill-note]')).toEqual([
      'admin.scaleway_bill.notes.pages',
    ]);
  });

  it('reads in French, the time of the reading and the units by their names', async () => {
    const { page } = await openPanelInFrench(BACKUPS.panel, BACKUPS.url, [
      BACKUPS.report,
      { path: BILL, body: scalewayBillReport() },
    ]);

    expect(texts(page, '[data-bill-fetched]')).toEqual([
      'Relevé chez Scaleway le 20/10/2026 18:00.',
    ]);
    expect(texts(page, 'tr[data-bill-line] td:nth-child(3)')).toEqual(
      expect.arrayContaining(['196 Go·h']),
    );
    // A deduction's quantity, always -1 in a currency, says nothing: only its amount shows.
    expect(texts(page, 'tr[data-bill-line][data-deduction] td:nth-child(3)')).toEqual(['', '']);
  });

  it('dates a stale bill in French, by the time of its reading', async () => {
    const { page } = await openPanelInFrench(BACKUPS.panel, BACKUPS.url, [
      BACKUPS.report,
      { path: BILL, body: scalewayBillReport({ stale: true }) },
    ]);

    expect(texts(page, '[data-bill-stale] span')).toEqual([
      "Scaleway n'a pas répondu à la dernière lecture : montants lus le 20/10/2026 18:00.",
    ]);
  });
});
