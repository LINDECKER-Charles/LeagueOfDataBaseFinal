import { HttpTestingController } from '@angular/common/http/testing';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { prerenderReport } from '../../../../testing/fixtures/prerender-report';
import { scalewayBillReport } from '../../../../testing/fixtures/scaleway-bill-report';
import { sent } from '../../../../testing/http/sent';
import type { LaunchPreset } from '../common/scope/launch-preset';
import type { LaunchLock } from '../overview/state/launch-lock-of';
import { CostsTab } from './costs-tab';

const BILL = '/api/admin/scaleway/bill';
const BASE = prerenderReport({ current: null });
const STANDBY = { ...BASE.storage.epochs[0], epoch: 3, state: 'standby', isWriting: false };
const REPORT: PrerenderReport = {
  ...BASE,
  storage: { ...BASE.storage, epochs: [STANDBY, ...BASE.storage.epochs] },
};

type Fixture = ComponentFixture<CostsTab>;

async function open(lock: LaunchLock | null = null) {
  configureAdminTestBed();
  const api = { http: TestBed.inject(HttpTestingController) };
  const fixture = TestBed.createComponent(CostsTab);
  fixture.componentRef.setInput('report', REPORT);
  fixture.componentRef.setInput('lock', lock);
  const presets: LaunchPreset[] = [];
  fixture.componentInstance.launch.subscribe((preset) => presets.push(preset));
  fixture.detectChanges();
  (await sent(api, BILL)).flush(scalewayBillReport());
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, presets, page: fixture.nativeElement as HTMLElement };
}

function cells(page: HTMLElement, selector: string): string[] {
  return [...page.querySelectorAll(`${selector} :is(th, td)`)].map((cell) =>
    (cell.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

function press(fixture: Fixture, selector: string): void {
  (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(selector)?.click();
  fixture.detectChanges();
}

describe('CostsTab', () => {
  afterEach(() =>
    document.querySelectorAll('.cdk-overlay-container').forEach((node) => node.remove()),
  );

  it('says the share of an epoch the automatic queue wrote, once it wrote some', async () => {
    const { fixture, page } = await open();
    expect(page.querySelector('[data-queue-share]')).toBeNull();

    const epochs = REPORT.storage.epochs.map((epoch) =>
      epoch.epoch === 2 ? { ...epoch, queuePages: 1_790 } : epoch,
    );
    fixture.componentRef.setInput('report', { ...REPORT, storage: { ...REPORT.storage, epochs } });
    fixture.detectChanges();

    const shares = page.querySelectorAll('[data-queue-share]');
    expect(shares).toHaveLength(1);
    expect(page.querySelector('tr[data-epoch="2"] [data-queue-share]')).not.toBeNull();
  });

  it('lists the epochs with their coverage, volume, cost and what each offers', async () => {
    const { page } = await open();

    expect(cells(page, 'tr[data-epoch="2"]')).toEqual([
      'admin.prerender.costs.epoch',
      'admin.prerender.states.served admin.prerender.states.writing',
      '98 %',
      '91 230',
      '9.24 GB',
      '0,07 €',
      'admin.prerender.costs.complete',
    ]);
    expect(cells(page, 'tr[data-epoch="3"]').at(-1)).toBe(
      'admin.prerender.costs.switch admin.prerender.costs.complete',
    );
    expect(cells(page, 'tr[data-epoch="1"]').slice(2)).toEqual([
      '—',
      '2 400',
      '240.00 MB',
      '< 0,01 €',
      'admin.prerender.costs.retire',
    ]);
    expect(cells(page, 'tr[data-total]').slice(2, 5)).toEqual(['91 230', '9.26 GB', '0,07 €']);
  });

  it('keeps the notes on the estimate, projects a new epoch and shows the bill', async () => {
    const { page } = await open();

    for (const note of ['prices', 'egress', 'counters', 'retired', 'bill-note']) {
      expect(page.querySelector(`[data-${note}]`)).not.toBeNull();
    }
    expect(page.querySelector('[data-projection]')?.textContent).toContain(
      'admin.prerender.costs.projection',
    );
    expect(page.querySelector('lodb-scaleway-bill')).not.toBeNull();
  });

  it('completes an epoch with gaps', async () => {
    const { fixture, presets } = await open();

    press(fixture, 'tr[data-epoch="2"] [data-complete]');

    expect(presets).toEqual([
      {
        origin: 'complete-epoch',
        versions: 'incomplete',
        priority: [],
        locales: null,
        pageTypes: null,
        epoch: 2,
        skipComplete: true,
        fromPass: null,
      },
    ]);
  });

  it('holds the completion while a pass is active, saying why', async () => {
    const { page } = await open({ kind: 'active', id: 12 });

    expect(page.querySelector<HTMLButtonElement>('[data-complete]')?.disabled).toBe(true);
    expect(page.querySelector('#prerender-costs-lock')).not.toBeNull();
  });

  it('prepares the switch to a standby epoch, and the retirement of a retired one', async () => {
    const { fixture } = await open();

    press(fixture, 'tr[data-epoch="3"] [data-switch]');
    await fixture.whenStable();
    expect(document.querySelector('lodb-switch-dialog')).not.toBeNull();

    press(fixture, 'tr[data-epoch="1"] [data-retire]');
    await fixture.whenStable();
    expect(document.querySelector('lodb-retire-dialog')).not.toBeNull();
  });
});
