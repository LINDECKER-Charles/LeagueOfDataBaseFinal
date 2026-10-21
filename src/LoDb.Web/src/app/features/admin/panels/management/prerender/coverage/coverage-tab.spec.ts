import { HttpTestingController } from '@angular/common/http/testing';
import { type ComponentFixture, TestBed } from '@angular/core/testing';
import type { PrerenderPatchRow } from '../../../../../../core/api/generated/models/prerender-patch-row';
import type { PrerenderReport } from '../../../../../../core/api/generated/models/prerender-report';
import { configureAdminTestBed } from '../../../../testing/admin-test-bed';
import { prerenderPatches } from '../../../../testing/fixtures/prerender-patches';
import { prerenderReport } from '../../../../testing/fixtures/prerender-report';
import { sent } from '../../../../testing/http/sent';
import type { LaunchPreset } from '../common/scope/launch-preset';
import type { LaunchLock } from '../overview/state/launch-lock-of';
import { CoverageTab } from './coverage-tab';

const PATCHES = '/api/admin/prerender/patches';
const [WHOLE, UNDER_WAY] = prerenderPatches().patches;
const ROWS: readonly PrerenderPatchRow[] = [
  WHOLE,
  UNDER_WAY,
  { ...WHOLE, version: '15.24.1' },
  { ...WHOLE, version: '15.23.1' },
];
const STANDBY = { ...prerenderReport().storage.epochs[0], epoch: 3, state: 'standby' };
const REPORT = prerenderReport({
  current: null,
  storage: {
    ...prerenderReport().storage,
    epochs: [STANDBY, ...prerenderReport().storage.epochs],
  },
});

type Fixture = ComponentFixture<CoverageTab>;

async function settle(fixture: Fixture): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
}

async function open(lock: LaunchLock | null = null, report: PrerenderReport = REPORT) {
  configureAdminTestBed();
  const api = { http: TestBed.inject(HttpTestingController) };
  const fixture = TestBed.createComponent(CoverageTab);
  fixture.componentRef.setInput('report', report);
  fixture.componentRef.setInput('lock', lock);
  const presets: LaunchPreset[] = [];
  fixture.componentInstance.launch.subscribe((preset) => presets.push(preset));
  fixture.detectChanges();
  (await sent(api, PATCHES)).flush(prerenderPatches({ patches: [...ROWS] }));
  await settle(fixture);
  return { fixture, api, presets, page: fixture.nativeElement as HTMLElement };
}

function versions(page: HTMLElement): (string | null)[] {
  return [...page.querySelectorAll('tr[data-version]')].map((row) =>
    row.getAttribute('data-version'),
  );
}

function click(fixture: Fixture, selector: string, init: MouseEventInit = {}): void {
  const target = (fixture.nativeElement as HTMLElement).querySelector(selector);
  if (!target) {
    throw new Error(`Nothing matches ${selector}.`);
  }
  target.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, ...init }));
  fixture.detectChanges();
}

describe('CoverageTab', () => {
  it('sums up the epoch read, then lists its patches by season, the whole ones folded', async () => {
    const { page } = await open();

    expect(page.querySelector('[data-coverage-summary]')?.textContent).toContain(
      'admin.prerender.coverage.summary',
    );
    expect(page.querySelector('[data-latest]')).not.toBeNull();
    expect(
      [...page.querySelectorAll('tbody[data-season]')].map((body) =>
        body.getAttribute('data-season'),
      ),
    ).toEqual(['16', '15']);
    expect(versions(page)).toEqual(['16.19.1', '16.18.1']);
    expect(
      page.querySelector('[data-season="15"] [aria-expanded]')?.getAttribute('aria-expanded'),
    ).toBe('false');
  });

  it('unfolds a season on demand', async () => {
    const { fixture, page } = await open();

    click(fixture, '[data-season="15"] button[aria-expanded]');

    expect(versions(page)).toEqual(['16.19.1', '16.18.1', '15.24.1', '15.23.1']);
  });

  it('filters by text and by status', async () => {
    const { fixture, page } = await open();
    const filter = page.querySelector<HTMLInputElement>('input[type="search"]');
    if (filter) {
      filter.value = '16.18';
      filter.dispatchEvent(new Event('input'));
    }
    fixture.detectChanges();
    expect(versions(page)).toEqual(['16.18.1']);

    if (filter) {
      filter.value = '';
      filter.dispatchEvent(new Event('input'));
    }
    click(fixture, '[data-status="partial"]');
    expect(versions(page)).toEqual(['16.18.1']);
    expect(page.querySelectorAll('tbody[data-season]')).toHaveLength(1);
  });

  it('ticks a range with Shift, shows the season in part, then pre-renders the selection', async () => {
    const { fixture, page, presets } = await open();

    click(fixture, '[data-version="16.19.1"] input');
    const season = page.querySelector<HTMLInputElement>('[data-season="16"] tr:first-child input');
    expect(season?.indeterminate).toBe(true);
    click(fixture, '[data-version="16.18.1"] input', { shiftKey: true });
    expect(season?.checked).toBe(true);
    expect(page.querySelector('[data-selection-bar]')?.textContent).toContain(
      'admin.prerender.coverage.selection',
    );
    click(fixture, '[data-launch-selection]');

    expect(presets).toEqual([
      {
        origin: 'coverage',
        versions: ['16.19.1', '16.18.1'],
        priority: [],
        locales: null,
        pageTypes: null,
        epoch: 2,
        skipComplete: true,
        fromPass: null,
      },
    ]);
  });

  it('ticks a whole season, and clears the selection', async () => {
    const { fixture, page } = await open();

    click(fixture, '[data-season="15"] tr:first-child input');
    expect(page.querySelector('[data-selection-bar]')).not.toBeNull();
    click(fixture, '[data-selection-bar] button');

    expect(page.querySelector('[data-selection-bar]')).toBeNull();
  });

  it('completes the gaps of the epoch read', async () => {
    const { fixture, presets } = await open();

    click(fixture, '[data-complete-gaps]');

    expect(presets[0]).toMatchObject({
      origin: 'complete-epoch',
      versions: 'incomplete',
      epoch: 2,
    });
  });

  it('holds the launches while a pass is active, saying why', async () => {
    const { fixture, page, presets } = await open({ kind: 'active', id: 12 });
    click(fixture, '[data-version="16.19.1"] input');

    expect(page.querySelector<HTMLButtonElement>('[data-complete-gaps]')?.disabled).toBe(true);
    expect(page.querySelector<HTMLButtonElement>('[data-launch-selection]')?.disabled).toBe(true);
    expect(page.querySelector('#prerender-coverage-lock')?.textContent).toContain(
      'admin.prerender.overview.cta.locked_active',
    );
    expect(presets).toEqual([]);
  });

  it('reads another epoch when asked', async () => {
    const { fixture, api, page } = await open();
    const [standby, served] = [...page.querySelectorAll('[data-epoch-option]')];
    expect(served.getAttribute('aria-pressed')).toBe('true');
    expect(page.querySelectorAll('[data-epoch-option]')).toHaveLength(2);

    click(fixture, '[data-epoch-option="3"]');
    const read = await sent(api, PATCHES);
    expect(read.request.params.get('epoch')).toBe('3');
    read.flush(prerenderPatches({ epoch: 3, patches: [...ROWS] }));
    await settle(fixture);

    expect(standby.getAttribute('aria-pressed')).toBe('true');
  });

  it('shows the matrix of the epoch', async () => {
    const { fixture, page } = await open();

    click(fixture, 'button[aria-pressed="false"]:not([data-epoch-option]):not([data-status])');

    expect(page.querySelector('lodb-unit-matrix')).not.toBeNull();
    expect(page.querySelector('table tr[data-version]')).toBeNull();
  });

  it('says the coverage could not be read, and reads it again', async () => {
    configureAdminTestBed();
    const api = { http: TestBed.inject(HttpTestingController) };
    const fixture = TestBed.createComponent(CoverageTab);
    fixture.componentRef.setInput('report', REPORT);
    fixture.componentRef.setInput('lock', null);
    fixture.detectChanges();
    (await sent(api, PATCHES)).flush(null, { status: 502, statusText: 'Bad Gateway' });
    await settle(fixture);

    expect(fixture.nativeElement.querySelector('[data-coverage-error]')).not.toBeNull();
    click(fixture, '[data-coverage-error] button');
    await sent(api, PATCHES);
  });
});
